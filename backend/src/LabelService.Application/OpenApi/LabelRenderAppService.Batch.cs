using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using LabelService.Application.ApiClients;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Diagnostics;
using LabelService.Rendering.Validation;

namespace LabelService.Application.OpenApi;

/// <summary>
/// 开放接口的批量生成（设计书第 8 节“批量生成”）。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>先校验全部条目，任何一条不通过整批返回，错误明细带条目序号 index。</item>
/// <item>merge：一个文件（PDF 多页、ZPL 多个 ^XA…^XZ），按 items 顺序。PNG 只能 zip。</item>
/// <item>zip：每个条目一个文件，并行渲染后按顺序打包，文件名 <c>模板编码_序号.扩展名</c>；PDF、ZPL 的份数在文件里，
/// PNG 一个文件只能放一张，所以每份一个文件。</item>
/// <item>各阶段耗时是全部张数的累计值，张数是总张数（含份数）。</item>
/// <item>带 layout 时按版式拼版：output 不起作用，PDF 一个文件、PNG 一页一张图（多页打包 ZIP），另有页数。</item>
/// </list>
/// </remarks>
public sealed partial class LabelRenderAppService
{
    /// <inheritdoc />
    public async ValueTask<OpenApiOutcome<RenderedFile>> RenderBatchAsync(AuthenticatedClient client, BatchRenderApiRequest? request, RenderTrace trace, CancellationToken cancellationToken)
    {
        // 第一步：只看请求本身就能发现的问题。
        if (string.IsNullOrWhiteSpace(request?.TemplateCode) || request.Items is not { Count: > 0 } items)
        {
            return new OpenApiError(ErrorCodes.InvalidJson, "请求体缺少 templateCode 或 items");
        }

        trace.TemplateCode = request.TemplateCode;
        if (!LabelFormats.TryParse(request.Format, out var format))
        {
            return new OpenApiError(ErrorCodes.FormatInvalid, FormatRule);
        }

        trace.Format = format;
        if (!LabelFormats.TryParseOutput(request.Output, out var output))
        {
            return new OpenApiError(ErrorCodes.OutputInvalid, "output 只能是 merge 或 zip");
        }

        if (SheetParameters.Check(trace, request.Layout, request.StartSlot, format) is { } sheetError)
        {
            return sheetError;
        }

        var sheet = !string.IsNullOrEmpty(request.Layout);
        if (format == LabelFormat.Png && output == BatchOutput.Merge && !sheet)
        {
            return new OpenApiError(ErrorCodes.OutputInvalid, "PNG 不能合并成一个文件，批量 PNG 请用 output=zip");
        }

        if (request.Dpi is { } requestedDpi && !RenderLimits.AllowedDpi.Contains(requestedDpi))
        {
            return new OpenApiError(ErrorCodes.DpiInvalid, DpiRule);
        }

        if (CheckCopies(items) is { } copiesError)
        {
            return copiesError;
        }

        var total = items.Sum(item => item?.Copies ?? 1);
        if (total > RenderLimits.MaxBatchLabels)
        {
            return new OpenApiError(ErrorCodes.BatchTooLarge, $"总张数（含份数）{total} 超过上限 {RenderLimits.MaxBatchLabels}，请拆成多批");
        }

        if (!renderer.Supports(format))
        {
            return new OpenApiError(OpenApiError.NotImplemented, $"输出格式 {request.Format} 尚未实现", StatusCode: 501);
        }

        // 第二步：取模板（含授权）和版式。
        trace.Mark(TimingStages.Validate);
        if (ResolveTemplate(client, request.TemplateCode, request.Version, trace, out var published) is { } templateError)
        {
            return templateError;
        }

        if (SheetParameters.Resolve(layouts, request.Layout, request.StartSlot, published.Template, out var layout) is { } layoutError)
        {
            return layoutError;
        }

        // 第三步：逐条校验数据（公共字段 + 条目字段），收集全部错误一次返回。
        trace.Mark(TimingStages.Template);
        var template = published.Template;
        var labels = new List<LabelInstance>(items.Count);
        var errors = new List<ErrorDetail>();
        var empty = new Dictionary<string, JsonElement>();

        // 自动值：整批共用一个当前时间，index 是条目序号，total 是条目数
        var auto = clock.For(template.Fields, items.Count, client.Name, trace.RequestId);
        for (var index = 0; index < items.Count; index++)
        {
            var validation = FieldValidator.Validate(template.Fields, request.Common, items[index]?.Data ?? empty, index, auto);
            errors.AddRange(validation.Errors);
            labels.Add(new LabelInstance(validation.Values, items[index]?.Copies ?? 1));
        }

        trace.Mark(TimingStages.Validate);
        if (errors.Count > 0)
        {
            return new OpenApiError(errors[0].Code, errors[0].Message ?? errors[0].Code, errors);
        }

        // 第四步：拿到并发名额后渲染。
        trace.Dpi = request.Dpi ?? DefaultDpi(template);
        using var slot = await gate.EnterAsync(cancellationToken);
        if (slot is null)
        {
            return OpenApiError.Busy(gate.TimeoutSeconds);
        }

        try
        {
            if (output == BatchOutput.Merge || layout is not null)
            {
                var job = new RenderJob
                {
                    Template = template,
                    Labels = labels,
                    Format = format,
                    Dpi = trace.Dpi,
                    Mono = request.Mono ?? false,
                    Layout = layout,
                    StartSlot = request.StartSlot ?? 1,
                    Timings = trace.Timings,
                };
                return ToFile(renderer.Render(job), published.Version);
            }

            var (content, warnings) = RenderZip(template, labels, format, trace.Dpi, request.Mono ?? false, trace.Timings);
            return new RenderedFile(content, LabelFormats.ZipContentType, "zip", total, trace.Dpi, warnings, published.Version, null);
        }
        catch (LabelRenderException ex)
        {
            // 批量只有一条时渲染引擎不加序号，这里补上，批量的错误总是带序号。
            var error = ex.LabelIndex is null ? ex.ForLabel(0) : ex;
            ErrorDetail[] detail = [new ErrorDetail { Index = error.LabelIndex, Code = error.ErrorCode, Message = error.Message }];
            return new OpenApiError(error.ErrorCode, error.Message, detail);
        }
    }

    /// <summary>
    /// 检查每条的份数。
    /// </summary>
    /// <param name="items">批量条目。</param>
    /// <returns>有份数超出范围的条目时返回错误（明细列出全部），否则 null。</returns>
    private static OpenApiError? CheckCopies(List<BatchRenderApiItem> items)
    {
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
        return copyErrors.Count > 0 ? new OpenApiError(ErrorCodes.CopiesInvalid, copyErrors[0].Message!, copyErrors) : null;
    }

    /// <summary>
    /// 并行渲染每个条目，再按顺序打成 zip。各阶段耗时累加到 <paramref name="timings"/>。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="labels">校验过的条目。</param>
    /// <param name="format">输出格式。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="mono">是否黑白。</param>
    /// <param name="timings">调用的分阶段耗时。</param>
    /// <returns>ZIP 内容和全部条目的渲染提示（去重）。</returns>
    /// <exception cref="LabelRenderException">某条渲染失败（报序号最小的那条）。</exception>
    private (byte[] Content, IReadOnlyCollection<string> Warnings) RenderZip(
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

        // 各条目的阶段耗时累加起来。
        foreach (var part in parts)
        {
            foreach (var (name, milliseconds) in part.Stages)
            {
                timings.Add(name, milliseconds);
            }
        }

        // 按顺序打包；PNG 本身已压缩，不再压缩。
        var started = Stopwatch.GetTimestamp();
        var extension = LabelFormats.Extension(format);
        var digits = Math.Max(3, results.Length.ToString(CultureInfo.InvariantCulture).Length);
        var level = format == LabelFormat.Png ? CompressionLevel.NoCompression : CompressionLevel.Fastest;
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < results.Length; i++)
            {
                var entry = archive.CreateEntry($"{template.Code}_{(i + 1).ToString("D" + digits, CultureInfo.InvariantCulture)}.{extension}", level);
                using var stream = entry.Open();
                stream.Write(results[i].Content.Span);
            }
        }

        timings.Mark(TimingStages.Encode, started);
        var warnings = results.SelectMany(result => result.Warnings).Distinct(StringComparer.Ordinal).ToList();
        return (buffer.ToArray(), warnings);
    }
}
