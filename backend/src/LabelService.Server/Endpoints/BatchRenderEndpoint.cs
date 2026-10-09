using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Diagnostics;
using LabelService.Rendering.Validation;
using LabelService.Server.Auth;
using LabelService.Server.Diagnostics;
using LabelService.Server.Hosting;
using LabelService.Server.Logging;
using LabelService.Server.Templates;

namespace LabelService.Server.Endpoints;

/// <summary>
/// POST /api/v1/render/batch：批量生成（设计书第 8 节“批量生成”）。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>先校验全部条目，任何一条不通过整批返回，错误明细带条目序号 index。</item>
/// <item>merge：一个文件（PDF 多页、ZPL 多个 ^XA…^XZ），按 items 顺序。PNG 只能 zip。</item>
/// <item>zip：每个条目一个文件，并行渲染后按顺序打包，文件名 <c>模板编码_序号.扩展名</c>；PDF、ZPL 的份数在文件里，
/// PNG 一个文件只能放一张，所以每份一个文件。</item>
/// <item>Server-Timing 各阶段是全部张数的累计值，X-Label-Count 是总张数（含份数）。</item>
/// </list>
/// </remarks>
internal static class BatchRenderEndpoint
{
    /// <summary>
    /// 处理批量请求。
    /// </summary>
    public static async Task<IResult> RenderBatchAsync(
        HttpContext http,
        ApiKeyAuthenticator authenticator,
        ITemplateStore store,
        ILabelRenderer renderer,
        RenderGate gate,
        LabelMetrics metrics,
        RenderLogWriter renderLog,
        ILogger<RenderCall> logger)
    {
        using var call = new RenderCall(http, metrics, logger, "batch", renderLog);
        var client = authenticator.Authenticate(http.Request.Headers[HeaderNames.ApiKey]);
        var stage = call.Timings.Mark(TimingStages.Auth, call.Started);
        if (client is null)
        {
            return call.Fail(ErrorCodes.Unauthorized, "API Key 缺失或无效");
        }

        call.Client = client.Name;
        call.ClientId = client.Id;
        if (!client.AllowsIp(http.Connection.RemoteIpAddress))
        {
            return call.Fail(ErrorCodes.Unauthorized, $"来源 IP {http.Connection.RemoteIpAddress} 不在调用方 {client.Name} 的白名单里");
        }
        var (body, readError) = await PublicApiEndpoints.ReadJsonAsync<BatchRenderApiRequest>(http);
        if (readError is not null)
        {
            return call.Fail(readError.Value.Code, readError.Value.Message, statusCode: readError.Value.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body?.TemplateCode) || body.Items is not { Count: > 0 } items)
        {
            return call.Fail(ErrorCodes.InvalidJson, "请求体缺少 templateCode 或 items");
        }

        call.TemplateCode = body.TemplateCode;
        if (!LabelFormats.TryParse(body.Format, out var format))
        {
            return call.Fail(ErrorCodes.FormatInvalid, "format 只能是 pdf、png、zpl");
        }

        call.Format = format;
        if (!LabelFormats.TryParseOutput(body.Output, out var output))
        {
            return call.Fail(ErrorCodes.OutputInvalid, "output 只能是 merge 或 zip");
        }

        if (format == LabelFormat.Png && output == BatchOutput.Merge)
        {
            return call.Fail(ErrorCodes.OutputInvalid, "PNG 不能合并成一个文件，批量 PNG 请用 output=zip");
        }

        if (body.Dpi is { } requestedDpi && !RenderLimits.AllowedDpi.Contains(requestedDpi))
        {
            return call.Fail(ErrorCodes.DpiInvalid, "dpi 只能是 203、300、600");
        }

        var copyErrors = items
            .Select((item, index) => (Index: index, Copies: item?.Copies ?? 1))
            .Where(item => item.Copies is < RenderLimits.MinCopies or > RenderLimits.MaxCopies)
            .Select(item => new ErrorDetail
            {
                Index = item.Index,
                Code = ErrorCodes.CopiesInvalid,
                Message = $"第 {item.Index + 1} 条的 copies 应在 {RenderLimits.MinCopies}–{RenderLimits.MaxCopies}",
            })
            .ToList();
        if (copyErrors.Count > 0)
        {
            return call.Fail(ErrorCodes.CopiesInvalid, copyErrors[0].Message!, copyErrors);
        }

        var total = items.Sum(item => item?.Copies ?? 1);
        if (total > RenderLimits.MaxBatchLabels)
        {
            return call.Fail(ErrorCodes.BatchTooLarge, $"总张数（含份数）{total} 超过上限 {RenderLimits.MaxBatchLabels}，请拆成多批");
        }

        if (!renderer.Supports(format))
        {
            return call.Fail(ApiErrors.NotImplemented, $"输出格式 {body.Format} 尚未实现", statusCode: 501);
        }

        stage = call.Timings.Mark(TimingStages.Validate, stage);
        if (!store.TryGetPublished(body.TemplateCode, body.Version, out var published))
        {
            return call.Fail(ErrorCodes.TemplateNotFound, $"模板 {body.TemplateCode} 不存在、版本不存在或还没有发布");
        }

        call.TemplateVersion = published.Version;

        if (!client.CanUse(published))
        {
            return call.Fail(ErrorCodes.TemplateForbidden, $"调用方 {client.Name} 无权使用模板 {body.TemplateCode}");
        }

        stage = call.Timings.Mark(TimingStages.Template, stage);
        var template = published.Template;
        var labels = new List<LabelInstance>(items.Count);
        var errors = new List<ErrorDetail>();
        var empty = new Dictionary<string, JsonElement>();
        for (var index = 0; index < items.Count; index++)
        {
            var validation = FieldValidator.Validate(template.Fields, body.Common, items[index]?.Data ?? empty, index);
            errors.AddRange(validation.Errors);
            labels.Add(new LabelInstance(validation.Values, items[index]?.Copies ?? 1));
        }

        call.Timings.Mark(TimingStages.Validate, stage);
        if (errors.Count > 0)
        {
            return call.Fail(errors[0].Code, errors[0].Message ?? errors[0].Code, errors);
        }

        call.Dpi = body.Dpi ?? (template.Page.Dpi > 0 ? template.Page.Dpi : RenderLimits.DefaultDpi);
        using var slot = await gate.EnterAsync(http.RequestAborted);
        if (slot is null)
        {
            return ApiErrors.Busy(call, http, gate.TimeoutSeconds);
        }

        try
        {
            if (output == BatchOutput.Merge)
            {
                var job = new RenderJob { Template = template, Labels = labels, Format = format, Dpi = call.Dpi, Mono = body.Mono ?? false, Timings = call.Timings };
                return call.Succeed(renderer.Render(job), published.Version);
            }

            var zip = RenderZip(renderer, template, labels, format, call.Dpi, body.Mono ?? false, call.Timings);
            return call.SucceedFile(zip.Content, LabelFormats.ZipContentType, "zip", total, call.Dpi, zip.Warnings, published.Version);
        }
        catch (LabelRenderException ex)
        {
            // 批量只有一条时渲染引擎不加序号，这里补上，批量的错误总是带序号。
            var error = ex.LabelIndex is null ? ex.ForLabel(0) : ex;
            ErrorDetail[] detail = [new ErrorDetail { Index = error.LabelIndex, Code = error.ErrorCode, Message = error.Message }];
            return call.Fail(error.ErrorCode, error.Message, detail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "批量渲染失败 template={Template} requestId={RequestId}", call.TemplateCode, call.RequestId);
            return call.Fail(ErrorCodes.RenderFailed, "渲染内部错误，请凭 traceId 联系标签服务管理员");
        }
    }

    /// <summary>
    /// 并行渲染每个条目，再按顺序打成 zip。各阶段耗时累加到 <paramref name="timings"/>。
    /// </summary>
    private static (byte[] Content, IReadOnlyCollection<string> Warnings) RenderZip(
        ILabelRenderer renderer,
        LabelTemplate template,
        List<LabelInstance> labels,
        LabelFormat format,
        int dpi,
        bool mono,
        RenderTimings timings)
    {
        // PNG 一个文件只能放一张：每份展开成一个条目。
        var units = format == LabelFormat.Png
            ? labels.SelectMany((label, index) => Enumerable.Repeat((Index: index, Label: label with { Copies = 1 }), label.Copies)).ToList()
            : labels.Select((label, index) => (Index: index, Label: label)).ToList();
        var results = new RenderResult[units.Count];
        var parts = new RenderTimings[units.Count];
        try
        {
            Parallel.For(0, units.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                parts[i] = new RenderTimings();
                try
                {
                    results[i] = renderer.Render(new RenderJob
                    {
                        Template = template,
                        Labels = [units[i].Label],
                        Format = format,
                        Dpi = dpi,
                        Mono = mono,
                        Timings = parts[i],
                    });
                }
                catch (LabelRenderException ex)
                {
                    throw ex.ForLabel(units[i].Index);
                }
            });
        }
        catch (AggregateException ex)
        {
            // 报序号最小的那条，和合并模式一致。
            var first = ex.InnerExceptions.OfType<LabelRenderException>().OrderBy(item => item.LabelIndex).FirstOrDefault();
            throw first ?? ex.InnerExceptions[0];
        }

        foreach (var part in parts)
        {
            foreach (var (name, milliseconds) in part.Stages)
            {
                timings.Add(name, milliseconds);
            }
        }

        var started = Stopwatch.GetTimestamp();
        var extension = LabelFormats.Extension(format);
        var digits = Math.Max(3, results.Length.ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
        var level = format == LabelFormat.Png ? CompressionLevel.NoCompression : CompressionLevel.Fastest;
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < results.Length; i++)
            {
                var entry = archive.CreateEntry($"{template.Code}_{(i + 1).ToString("D" + digits, System.Globalization.CultureInfo.InvariantCulture)}.{extension}", level);
                using var stream = entry.Open();
                stream.Write(results[i].Content.Span);
            }
        }

        timings.Mark(TimingStages.Encode, started);
        var warnings = results.SelectMany(result => result.Warnings).Distinct(StringComparer.Ordinal).ToList();
        return (buffer.ToArray(), warnings);
    }
}
