using System.Diagnostics;
using System.Globalization;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Rendering.Diagnostics;
using LabelService.Server.Diagnostics;

namespace LabelService.Server.Endpoints;

/// <summary>
/// 一次生成请求的收尾工作：总耗时、Server-Timing 头、指标、调用日志。单张、批量、预览共用。
/// </summary>
/// <remarks>
/// 每个请求必须恰好结束一次：成功走 <see cref="Succeed"/>，失败走 <see cref="Fail"/>，
/// 异常或取消时由 <see cref="Dispose"/> 兜底，保证并发数指标不泄漏。
/// 调用日志目前只写 ILogger；render_log 表完成后（T-009）在 <see cref="Finish"/> 里一并写入。
/// </remarks>
internal sealed class RenderCall : IDisposable
{
    private readonly HttpContext http;
    private readonly LabelMetrics metrics;
    private readonly ILogger logger;
    private readonly string endpoint;
    private readonly long started = Stopwatch.GetTimestamp();
    private bool finished;

    /// <summary>
    /// 开始一次请求并计入并发数。
    /// </summary>
    public RenderCall(HttpContext http, LabelMetrics metrics, ILogger logger, string endpoint)
    {
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
        Finish(code, 0);
        return ApiErrors.Create(http, code, message, errors, statusCode);
    }

    /// <summary>
    /// 成功结束：写响应头，直接返回文件内容。
    /// </summary>
    public IResult Succeed(RenderResult result, int templateVersion)
    {
        Finish("ok", result.LabelCount);
        var headers = http.Response.Headers;
        headers[HeaderNames.TemplateVersion] = templateVersion.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.LabelCount] = result.LabelCount.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.LabelDpi] = result.Dpi.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.TraceId] = ApiErrors.TraceId(http);
        if (result.Warnings.Count > 0)
        {
            headers[HeaderNames.LabelWarnings] = string.Join(",", result.Warnings);
        }

        var fileName = $"{TemplateCode}_{DateTime.Now:yyyyMMdd}.{LabelFormats.Extension(result.Format)}";
        return Results.Bytes(result.Content, result.ContentType, fileName);
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
    }
}
