using System.Diagnostics;
using System.Globalization;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Rendering.Diagnostics;
using LabelService.Server.Data;
using LabelService.Server.Diagnostics;
using LabelService.Server.Logging;

namespace LabelService.Server.Endpoints;

/// <summary>
/// 一次生成请求的收尾工作：总耗时、Server-Timing 头、指标、调用日志。单张、批量、预览共用。
/// </summary>
/// <remarks>
/// 每个请求必须恰好结束一次：成功走 <see cref="Succeed"/>，失败走 <see cref="Fail"/>，
/// 异常或取消时由 <see cref="Dispose"/> 兜底，保证并发数指标不泄漏。
/// 结束时写一条 ILogger 日志，并投递一条调用日志（render_log，后台批量写入，不阻塞请求）。
/// </remarks>
internal sealed class RenderCall : IDisposable
{
    private readonly HttpContext http;
    private readonly LabelMetrics metrics;
    private readonly ILogger logger;
    private readonly string endpoint;
    private readonly RenderLogWriter? renderLog;
    private readonly long started = Stopwatch.GetTimestamp();
    private bool finished;
    private string? failureMessage;

    /// <summary>
    /// 开始一次请求并计入并发数。
    /// </summary>
    public RenderCall(HttpContext http, LabelMetrics metrics, ILogger logger, string endpoint, RenderLogWriter? renderLog = null)
    {
        this.renderLog = renderLog;
        this.http = http;
        this.metrics = metrics;
        this.logger = logger;
        this.endpoint = endpoint;
        metrics.RequestStarted();
    }

    /// <summary>请求开始时的时间戳。</summary>
    public long Started => started;

    /// <summary>分阶段耗时。</summary>
    public RenderTimings Timings { get; } = new();

    /// <summary>已识别的调用方名称。</summary>
    public string Client { get; set; } = "anonymous";

    /// <summary>调用方 ID（api_client.id）；配置文件里的调用方为 null。</summary>
    public int? ClientId
    {
        get; set;
    }

    /// <summary>实际使用的模板版本。</summary>
    public int? TemplateVersion
    {
        get; set;
    }

    /// <summary>模板编码。</summary>
    public string TemplateCode { get; set; } = "";

    /// <summary>输出格式。</summary>
    public LabelFormat? Format
    {
        get; set;
    }

    /// <summary>实际 DPI。</summary>
    public int Dpi
    {
        get; set;
    }

    /// <summary>调用方传来的 X-Request-Id，最长保留 64 个字符。</summary>
    public string RequestId
    {
        get
        {
            var value = http.Request.Headers[HeaderNames.RequestId].ToString();
            return value.Length > 64 ? value[..64] : value;
        }
    }

    /// <summary>
    /// 失败结束：写头、记指标和日志，返回统一错误响应。
    /// </summary>
    public IResult Fail(string code, string message, IReadOnlyList<ErrorDetail>? errors = null, int? statusCode = null)
    {
        failureMessage = message;
        Finish(code, 0);
        return ApiErrors.Create(http, code, message, errors, statusCode);
    }

    /// <summary>
    /// 成功：返回渲染出的文件，写响应头（版本、张数、DPI、警告、链路 ID）。
    /// </summary>
    public IResult Succeed(RenderResult result, int templateVersion) =>
        SucceedFile(result.Content, result.ContentType, LabelFormats.Extension(result.Format), result.LabelCount, result.Dpi, result.Warnings, templateVersion);

    /// <summary>
    /// 成功：返回任意文件（批量 zip 用），响应头和单张相同。
    /// </summary>
    /// <param name="content">文件内容。</param>
    /// <param name="contentType">Content-Type。</param>
    /// <param name="extension">下载文件名的扩展名。</param>
    /// <param name="labelCount">张数（含份数）。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="warnings">渲染警告。</param>
    /// <param name="templateVersion">模板版本。</param>
    public IResult SucceedFile(ReadOnlyMemory<byte> content, string contentType, string extension, int labelCount, int dpi, IReadOnlyCollection<string> warnings, int templateVersion)
    {
        Finish("ok", labelCount);
        var headers = http.Response.Headers;
        headers[HeaderNames.TemplateVersion] = templateVersion.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.LabelCount] = labelCount.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.LabelDpi] = dpi.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.TraceId] = ApiErrors.TraceId(http);
        if (warnings.Count > 0)
        {
            headers[HeaderNames.LabelWarnings] = string.Join(",", warnings);
        }

        var fileName = $"{TemplateCode}_{DateTime.Now:yyyyMMdd}.{extension}";
        return Results.Bytes(content, contentType, fileName);
    }

    /// <summary>
    /// 请求被取消或抛出未处理异常时兜底结束。
    /// </summary>
    public void Dispose()
    {
        if (!finished)
        {
            Finish("aborted", 0);
        }
    }

    private void Finish(string result, int labelCount)
    {
        finished = true;
        Timings.Mark(TimingStages.Total, started);
        http.Response.Headers[HeaderNames.ServerTiming] = Timings.ToServerTimingHeader();
        var format = Format is { } value ? LabelFormats.Extension(value) : "";
        metrics.RequestFinished(endpoint, Client, TemplateCode, format, Dpi, result, Timings, labelCount);

        var total = Timings.Get(TimingStages.Total);
        var slow = result == "ok" && total > RenderLimits.SlowRenderThresholdMs * Math.Max(1, labelCount);
        logger.Log(
            slow ? LogLevel.Warning : LogLevel.Information,
            "{Endpoint} {Result} client={Client} template={Template} format={Format} dpi={Dpi} labels={Labels} total={Total:0.0}ms slow={Slow} requestId={RequestId} timing={Timing}",
            endpoint,
            result,
            Client,
            TemplateCode,
            format,
            Dpi,
            labelCount,
            total,
            slow,
            RequestId,
            Timings.ToServerTimingHeader());
        renderLog?.TryWrite(new RenderLogRecord
        {
            RequestId = RequestId,
            TraceId = ApiErrors.TraceId(http),
            Ip = http.Connection.RemoteIpAddress?.ToString() ?? "",
            Endpoint = endpoint,
            ClientId = ClientId,
            ClientName = Client,
            TemplateCode = TemplateCode,
            VersionNo = TemplateVersion,
            Format = format,
            ItemCount = labelCount,
            DurationMs = Math.Round(total, 1),
            StageMsJson = RenderLogWriter.StagesJson(Timings),
            IsSlow = slow,
            Status = result is "ok" or "aborted" ? result : "error",
            ErrorCode = result is "ok" or "aborted" ? null : result,
            ErrorMessage = failureMessage is { Length: > 512 } ? failureMessage[..512] : failureMessage,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
