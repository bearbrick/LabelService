using System.Diagnostics;
using System.Globalization;
using LabelService.Application.ApiClients;
using LabelService.Application.Diagnostics;
using LabelService.Application.Logs;
using LabelService.Application.OpenApi;
using LabelService.Contracts.Protocol;
using LabelService.Domain.Logs;

namespace LabelService.Api.Endpoints.OpenApi;

/// <summary>
/// 开放接口一次请求的收尾工作：总耗时、Server-Timing 头、指标、调用日志。单张和批量共用。
/// </summary>
/// <remarks>
/// <para>
/// 计时口径（docs/performance.md）：服务端处理时间从开始处理请求算到写完最后一个字节。
/// 成功走 <see cref="Succeed"/>、失败走 <see cref="Fail(OpenApiError)"/> 时只记下结果并写 Server-Timing 头——响应头先于响应体发出，
/// 头里的 total 只能算到这一刻，不含写响应体；真正的总耗时在响应写完后（<c>HttpResponse.OnCompleted</c>）计算，
/// 指标、ILogger 日志、调用日志和慢调用判断都用这个总耗时。
/// </para>
/// <para>
/// 每个请求恰好结束一次：响应写完时由 OnCompleted 结束（包括抛异常、客户端断开），并发数指标不会泄漏。
/// </para>
/// </remarks>
internal sealed class RenderCall : IDisposable
{
    /// <summary>
    /// X-Request-Id 最多保留几个字符。
    /// </summary>
    private const int MaxRequestIdLength = 64;

    /// <summary>
    /// 调用日志里错误说明最多保留几个字符。
    /// </summary>
    private const int MaxErrorMessageLength = 512;

    /// <summary>
    /// 当前请求。
    /// </summary>
    private readonly HttpContext http;

    /// <summary>
    /// 指标。
    /// </summary>
    private readonly LabelMetrics metrics;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger logger;

    /// <summary>
    /// 接口名：render 或 batch。
    /// </summary>
    private readonly string endpoint;

    /// <summary>
    /// 调用日志队列。
    /// </summary>
    private readonly RenderLogQueue renderLog;

    /// <summary>
    /// 结果：ok、错误码或 aborted；还没结果时为 null。
    /// </summary>
    private string? outcome;

    /// <summary>
    /// 成功时的张数。
    /// </summary>
    private int outcomeLabels;

    /// <summary>
    /// 是否已经记过指标和日志。
    /// </summary>
    private bool completed;

    /// <summary>
    /// 失败时的说明，写进调用日志。
    /// </summary>
    private string? failureMessage;

    /// <summary>
    /// 开始一次请求并计入并发数。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="metrics">指标。</param>
    /// <param name="logger">日志。</param>
    /// <param name="endpoint">接口名：render 或 batch。</param>
    /// <param name="renderLog">调用日志队列。</param>
    public RenderCall(HttpContext http, LabelMetrics metrics, ILogger logger, string endpoint, RenderLogQueue renderLog)
    {
        this.http = http;
        this.metrics = metrics;
        this.logger = logger;
        this.endpoint = endpoint;
        this.renderLog = renderLog;
        Trace = new RenderTrace(Stopwatch.GetTimestamp());
        Trace.RequestId = RequestId is { Length: > 0 } requestId ? requestId : ApiErrors.TraceId(http);
        metrics.RequestStarted();
        http.Response.OnCompleted(() =>
        {
            Complete();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 调用记录：各阶段耗时和处理过程中确定的模板、版本、格式、版式、DPI。
    /// </summary>
    public RenderTrace Trace
    {
        get;
    }

    /// <summary>
    /// 已识别的调用方名称。
    /// </summary>
    public string Client { get; set; } = "anonymous";

    /// <summary>
    /// 通过鉴权的调用方；还没通过时为 null。
    /// </summary>
    public AuthenticatedClient? Authenticated
    {
        get;
        set;
    }

    /// <summary>
    /// 调用方 ID；配置文件里的调用方为 null。
    /// </summary>
    public int? ClientId
    {
        get;
        set;
    }

    /// <summary>
    /// 调用方传来的 X-Request-Id，最长保留 64 个字符。
    /// </summary>
    public string RequestId
    {
        get
        {
            var value = http.Request.Headers[HeaderNames.RequestId].ToString();
            return value.Length > MaxRequestIdLength ? value[..MaxRequestIdLength] : value;
        }
    }

    /// <summary>
    /// 失败：记下错误码、写 Server-Timing 头，返回统一错误响应。指标和日志在响应写完后记。
    /// </summary>
    /// <param name="error">错误。</param>
    /// <returns>错误响应；要求稍后重试的带 Retry-After: 1。</returns>
    public IResult Fail(OpenApiError error)
    {
        if (error.RetryLater)
        {
            http.Response.Headers.RetryAfter = "1";
        }

        return Fail(error.Code, error.Message, error.Errors, error.StatusCode);
    }

    /// <summary>
    /// 失败：记下错误码、写 Server-Timing 头，返回统一错误响应。
    /// </summary>
    /// <param name="code">错误码。</param>
    /// <param name="message">中文说明。</param>
    /// <param name="errors">明细，可空。</param>
    /// <param name="statusCode">HTTP 状态码；不传则按错误码取。</param>
    /// <returns>错误响应。</returns>
    public IResult Fail(string code, string message, IReadOnlyList<ErrorDetail>? errors = null, int? statusCode = null)
    {
        failureMessage = message;
        Settle(code, 0);
        return ApiErrors.Create(http, code, message, errors, statusCode);
    }

    /// <summary>
    /// 成功：返回生成的文件，写响应头（版本、张数、拼版页数、DPI、警告、链路 ID）。
    /// </summary>
    /// <param name="file">生成的文件。</param>
    /// <returns>文件响应，文件名是“模板编码_日期.扩展名”。</returns>
    public IResult Succeed(RenderedFile file)
    {
        Settle("ok", file.LabelCount);
        var headers = http.Response.Headers;
        headers[HeaderNames.TemplateVersion] = file.TemplateVersion.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.LabelCount] = file.LabelCount.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.LabelDpi] = file.Dpi.ToString(CultureInfo.InvariantCulture);
        if (file.PageCount is { } pages)
        {
            headers[HeaderNames.PageCount] = pages.ToString(CultureInfo.InvariantCulture);
        }

        headers[HeaderNames.TraceId] = ApiErrors.TraceId(http);
        if (file.Warnings.Count > 0)
        {
            headers[HeaderNames.LabelWarnings] = string.Join(",", file.Warnings);
        }

        var fileName = $"{Trace.TemplateCode}_{DateTime.Now:yyyyMMdd}.{file.Extension}";
        return Results.Bytes(file.Content, file.ContentType, fileName);
    }

    /// <summary>
    /// 处理函数结束时调用：没走到 Succeed、Fail（抛异常或取消）的记为 aborted，响应写完后照样结束。
    /// </summary>
    public void Dispose()
    {
        if (outcome is null)
        {
            Settle("aborted", 0);
        }
    }

    /// <summary>
    /// 记下结果，写 Server-Timing 头（total 到此刻为止，不含写响应体）。只记第一次。
    /// </summary>
    /// <param name="result">ok、错误码或 aborted。</param>
    /// <param name="labelCount">成功时的张数。</param>
    private void Settle(string result, int labelCount)
    {
        if (outcome is not null)
        {
            return;
        }

        outcome = result;
        outcomeLabels = labelCount;
        Trace.Timings.Mark(TimingStages.Total, Trace.Started);
        if (!http.Response.HasStarted)
        {
            http.Response.Headers[HeaderNames.ServerTiming] = Trace.Timings.ToServerTimingHeader();
        }
    }

    /// <summary>
    /// 响应写完（最后一个字节交给连接）后：总耗时改成到这一刻，记指标、日志、调用日志。
    /// </summary>
    private void Complete()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        var result = outcome ?? "aborted";
        var labelCount = result == "ok" ? outcomeLabels : 0;
        if (result == "ok" && http.RequestAborted.IsCancellationRequested)
        {
            // 客户端在响应写完前断开：文件没有送到。
            result = "aborted";
            labelCount = 0;
        }

        var timings = Trace.Timings;
        var total = Stopwatch.GetElapsedTime(Trace.Started).TotalMilliseconds;
        timings.Add(TimingStages.Total, total - timings.Get(TimingStages.Total));
        var format = Trace.Format is { } value ? LabelFormats.Extension(value) : "";
        metrics.RequestFinished(endpoint, Client, Trace.TemplateCode, format, Trace.Dpi, result, timings, labelCount);

        // 慢调用：平均每张超过 100 ms。
        var slow = result == "ok" && total > RenderLimits.SlowRenderThresholdMs * Math.Max(1, labelCount);
        logger.Log(
            slow ? LogLevel.Warning : LogLevel.Information,
            "{Endpoint} {Result} client={Client} template={Template} format={Format} dpi={Dpi} labels={Labels} total={Total:0.0}ms slow={Slow} requestId={RequestId} timing={Timing}",
            endpoint,
            result,
            Client,
            Trace.TemplateCode,
            format,
            Trace.Dpi,
            labelCount,
            total,
            slow,
            RequestId,
            timings.ToServerTimingHeader());
        renderLog.TryWrite(new RenderLogEntity
        {
            RequestId = RequestId,
            TraceId = ApiErrors.TraceId(http),
            Ip = http.Connection.RemoteIpAddress?.ToString() ?? "",
            Endpoint = endpoint,
            ClientId = ClientId,
            ClientName = Client,
            TemplateCode = Trace.TemplateCode,
            VersionNo = Trace.TemplateVersion,
            Format = format,
            LayoutCode = Trace.LayoutCode,
            ItemCount = labelCount,
            DurationMs = Math.Round(total, 1),
            StageMsJson = RenderLogQueue.StagesJson(timings),
            IsSlow = slow,
            Status = result is "ok" or "aborted" ? result : "error",
            ErrorCode = result is "ok" or "aborted" ? null : result,
            ErrorMessage = failureMessage is { Length: > MaxErrorMessageLength } ? failureMessage[..MaxErrorMessageLength] : failureMessage,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
