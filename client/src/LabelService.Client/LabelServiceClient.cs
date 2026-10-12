using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using LabelService.Client.Internal;

namespace LabelService.Client;

/// <summary>
/// 标签服务客户端。线程安全，应用内建一个实例共用，内部复用 HTTP 长连接。
/// </summary>
/// <remarks>
/// 网络错误、超时和 502、503、504 自动重试 <see cref="LabelServiceClientOptions.MaxRetries"/> 次；其余错误直接抛
/// <see cref="LabelServiceException"/>。每次调用自动带 X-Api-Key、User-Agent 和新的 X-Request-Id。
/// </remarks>
public sealed class LabelServiceClient : ILabelServiceClient, IDisposable
{
    /// <summary>
    /// 创建时传入的设置。
    /// </summary>
    private readonly LabelServiceClientOptions options;

    /// <summary>
    /// 内部的 HttpClient，BaseAddress 末尾补了斜杠，超时由每次调用自己控制。
    /// </summary>
    private readonly HttpClient http;

    /// <summary>
    /// 每次请求带的 User-Agent：设置里没给时是 LabelService.Client/版本号。
    /// </summary>
    private readonly string userAgent;

    /// <summary>
    /// 创建客户端。
    /// </summary>
    /// <param name="options">设置，必须有 BaseAddress 和 ApiKey。</param>
    public LabelServiceClient(LabelServiceClientOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// 用自定义 HttpMessageHandler 创建客户端，用于代理设置、单元测试或演示程序的模拟服务。
    /// </summary>
    /// <param name="options">设置，必须有 BaseAddress 和 ApiKey。</param>
    /// <param name="handler">消息处理器，由调用方负责释放；传 null 使用默认处理器。</param>
    public LabelServiceClient(LabelServiceClientOptions options, HttpMessageHandler? handler)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.BaseAddress == null)
        {
            throw new ArgumentException("必须设置 BaseAddress。", nameof(options));
        }

        if (options.ApiKey == null || options.ApiKey.Trim().Length == 0)
        {
            throw new ArgumentException("必须设置 ApiKey。", nameof(options));
        }

        this.options = options;
        http = handler == null ? new HttpClient(CreateDefaultHandler(), disposeHandler: true) : new HttpClient(handler, disposeHandler: false);
        var baseAddress = options.BaseAddress.ToString();
        http.BaseAddress = new Uri(baseAddress.EndsWith("/", StringComparison.Ordinal) ? baseAddress : baseAddress + "/");
        http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        var version = typeof(LabelServiceClient).Assembly.GetName().Version;
        userAgent = options.UserAgent ?? $"LabelService.Client/{version?.Major}.{version?.Minor}.{version?.Build}";
    }

    /// <inheritdoc />
    public event EventHandler<CallCompletedEventArgs>? CallCompleted;

    /// <inheritdoc />
    public Task<LabelFile> RenderAsync(RenderRequest request, CancellationToken cancellationToken = default)
    {
        var body = Payloads.Build(request);
        return SendAsync("Render", request.TemplateCode, HttpMethod.Post, "api/v1/render", body,
            (response, requestId) => ReadFileAsync(response, requestId, request.TemplateCode, request.Format), cancellationToken);
    }

    /// <inheritdoc />
    public LabelFile Render(RenderRequest request) => Task.Run(() => RenderAsync(request)).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task<LabelFile> RenderBatchAsync(BatchRenderRequest request, CancellationToken cancellationToken = default)
    {
        var body = Payloads.Build(request, options.MaxLabelsPerRequest);
        return SendAsync("RenderBatch", request.TemplateCode, HttpMethod.Post, "api/v1/render/batch", body,
            (response, requestId) => ReadFileAsync(response, requestId, request.TemplateCode, request.Format), cancellationToken);
    }

    /// <inheritdoc />
    public LabelFile RenderBatch(BatchRenderRequest request) => Task.Run(() => RenderBatchAsync(request)).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task<TemplateSchema> GetSchemaAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        if (templateCode == null || templateCode.Trim().Length == 0)
        {
            throw new ArgumentException("模板编码不能为空。", nameof(templateCode));
        }

        var path = "api/v1/templates/" + Uri.EscapeDataString(templateCode.Trim()) + "/schema";
        return SendAsync("GetSchema", templateCode, HttpMethod.Get, path, null,
            (response, requestId) => ReadJsonAsync<TemplateSchema>(response, requestId, "字段定义"), cancellationToken);
    }

    /// <inheritdoc />
    public TemplateSchema GetSchema(string templateCode) => Task.Run(() => GetSchemaAsync(templateCode)).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var body = await SendAsync("ListTemplates", "", HttpMethod.Get, "api/v1/templates", null,
            (response, requestId) => ReadJsonAsync<TemplateListBody>(response, requestId, "模板列表"), cancellationToken).ConfigureAwait(false);
        return body.Templates ?? new List<TemplateSummary>();
    }

    /// <inheritdoc />
    public IReadOnlyList<TemplateSummary> ListTemplates() => Task.Run(() => ListTemplatesAsync()).GetAwaiter().GetResult();

    /// <summary>
    /// 释放内部的 HttpClient。
    /// </summary>
    public void Dispose() => http.Dispose();

    /// <summary>
    /// 发一次调用：带上鉴权和链路头，按设置超时和重试，成功时交给 onSuccess 读响应，失败时转成 <see cref="LabelServiceException"/>。
    /// 无论成功失败都触发一次 <see cref="CallCompleted"/>。
    /// </summary>
    /// <typeparam name="T">调用结果的类型。</typeparam>
    /// <param name="operation">操作名，写进调用记录，如 Render。</param>
    /// <param name="templateCode">模板编码，写进调用记录。</param>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="path">相对 BaseAddress 的路径。</param>
    /// <param name="body">请求体（UTF-8 JSON）；GET 时为 null。</param>
    /// <param name="onSuccess">读成功响应的函数，参数是响应和链路 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>onSuccess 的结果。</returns>
    /// <exception cref="LabelServiceException">服务端返回错误、网络错误或超时（已按设置重试过）。</exception>
    private async Task<T> SendAsync<T>(
        string operation,
        string templateCode,
        HttpMethod method,
        string path,
        byte[]? body,
        Func<HttpResponseMessage, string, Task<T>> onSuccess,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var info = new CallCompletedEventArgs { Operation = operation, TemplateCode = templateCode, RequestId = requestId };
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // 同一次调用的重试共用一个 X-Request-Id，服务端日志里能看出是重试；每次尝试单独计超时。
            for (var attempt = 1; ; attempt++)
            {
                info.Attempts = attempt;
                info.StatusCode = null;
                LabelServiceException failure;
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(options.Timeout);
                    try
                    {
                        using (var message = CreateMessage(method, path, body, requestId))
                        using (var response = await http.SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false))
                        {
                            info.StatusCode = (int)response.StatusCode;
                            info.TraceId = Header(response, HeaderNames.TraceId);
                            info.ServerDuration = ServerDuration(ServerTimingParser.Parse(Header(response, HeaderNames.ServerTiming)));
                            if (response.IsSuccessStatusCode)
                            {
                                return await onSuccess(response, requestId).ConfigureAwait(false);
                            }

                            failure = await ReadErrorAsync(response, requestId).ConfigureAwait(false);
                        }
                    }
                    // 调用方取消时直接往外抛；只有自己的超时计时器触发的取消才算超时。
                    catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        failure = new LabelServiceException(
                            LabelErrorCodes.Timeout,
                            $"请求超过 {options.Timeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} 秒未完成",
                            null,
                            ex)
                        {
                            RequestId = requestId
                        };
                    }
                    catch (HttpRequestException ex)
                    {
                        failure = new LabelServiceException(LabelErrorCodes.NetworkError, "无法连接标签服务：" + ex.Message, null, ex) { RequestId = requestId };
                    }
                }

                // 只有网络错误、超时、502/503/504 重试；等待时间按次数线性增加。
                if (!failure.IsTransient || attempt > options.MaxRetries)
                {
                    info.ErrorCode = failure.ErrorCode;
                    throw failure;
                }

                await Task.Delay(TimeSpan.FromTicks(options.RetryDelay.Ticks * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (LabelServiceException ex)
        {
            info.ErrorCode = ex.ErrorCode;
            throw;
        }
        finally
        {
            info.Duration = stopwatch.Elapsed;
            RaiseCallCompleted(info);
        }
    }

    /// <summary>
    /// 建一条请求：带 API Key、链路 ID、User-Agent，有请求体时按 UTF-8 JSON 发送。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="path">相对 BaseAddress 的路径。</param>
    /// <param name="body">请求体；没有时为 null。</param>
    /// <param name="requestId">X-Request-Id。</param>
    /// <returns>请求消息，由调用方释放。</returns>
    private HttpRequestMessage CreateMessage(HttpMethod method, string path, byte[]? body, string requestId)
    {
        var message = new HttpRequestMessage(method, path);
        message.Headers.TryAddWithoutValidation(HeaderNames.ApiKey, options.ApiKey);
        message.Headers.TryAddWithoutValidation(HeaderNames.RequestId, requestId);
        message.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (body != null)
        {
            message.Content = new ByteArrayContent(body);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        return message;
    }

    /// <summary>
    /// 把成功的生成响应读成 <see cref="LabelFile"/>：文件内容、文件名和响应头里的版本、张数、页数、DPI、计时、警告。
    /// </summary>
    /// <param name="response">成功的响应。</param>
    /// <param name="requestId">这次调用的链路 ID。</param>
    /// <param name="templateCode">模板编码（响应没带文件名时用来起名）。</param>
    /// <param name="format">请求的输出格式。</param>
    /// <returns>生成的文件。</returns>
    private static async Task<LabelFile> ReadFileAsync(HttpResponseMessage response, string requestId, string templateCode, LabelFormat format)
    {
        var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "";
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? templateCode + "." + Payloads.FormatName(format);
        var timing = ServerTimingParser.Parse(Header(response, HeaderNames.ServerTiming));
        var warnings = Header(response, HeaderNames.LabelWarnings);
        return new LabelFile(content, contentType, fileName, format)
        {
            TemplateVersion = ParseInt(Header(response, HeaderNames.TemplateVersion)),
            LabelCount = ParseInt(Header(response, HeaderNames.LabelCount)) ?? 0,
            PageCount = ParseInt(Header(response, HeaderNames.PageCount)) ?? ParseInt(Header(response, HeaderNames.LabelCount)) ?? 0,
            Dpi = ParseInt(Header(response, HeaderNames.LabelDpi)) ?? 0,
            TraceId = Header(response, HeaderNames.TraceId),
            RequestId = requestId,
            ServerTiming = timing,
            ServerDuration = ServerDuration(timing),
            Warnings = warnings == null
                ? new string[0]
                : warnings.Split(',').Select(item => item.Trim()).Where(item => item.Length > 0).ToArray(),
        };
    }

    /// <summary>
    /// 把成功的 JSON 响应（字段定义、模板列表）反序列化。
    /// </summary>
    /// <typeparam name="T">响应体的类型。</typeparam>
    /// <param name="response">成功的响应。</param>
    /// <param name="requestId">这次调用的链路 ID。</param>
    /// <param name="what">响应的名称，写进解析失败的说明，如“字段定义”。</param>
    /// <returns>响应体。</returns>
    /// <exception cref="LabelServiceException">响应体不是预期的 JSON（UNEXPECTED_RESPONSE）。</exception>
    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, string requestId, string what)
        where T : class
    {
        var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<T>(content, ClientJson.Options)
                ?? throw new JsonException("响应体为空");
        }
        catch (JsonException ex)
        {
            throw new LabelServiceException(LabelErrorCodes.UnexpectedResponse, "无法解析" + what + "：" + ex.Message, (int)response.StatusCode, ex)
            {
                RequestId = requestId,
                TraceId = Header(response, HeaderNames.TraceId),
            };
        }
    }

    /// <summary>
    /// 把失败的响应转成异常：优先用响应体里的错误码和说明；网关错误统一算服务不可用，没有错误体时按状态码推断。
    /// </summary>
    /// <param name="response">失败的响应。</param>
    /// <param name="requestId">这次调用的链路 ID。</param>
    /// <returns>要抛出的异常（这里只构造，不抛）。</returns>
    private static async Task<LabelServiceException> ReadErrorAsync(HttpResponseMessage response, string requestId)
    {
        var status = (int)response.StatusCode;
        ErrorBody? body = null;
        try
        {
            var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            body = content.Length > 0 ? JsonSerializer.Deserialize<ErrorBody>(content, ClientJson.Options) : null;
        }
        catch (JsonException)
        {
            // 代理或网关返回的 HTML 错误页，按状态码处理。
        }

        var code = status == 502 || status == 503 || status == 504
            ? LabelErrorCodes.ServiceUnavailable
            : body?.Code ?? CodeFromStatus(status);
        var message = body?.Message ?? $"标签服务返回 HTTP {status}";
        return new LabelServiceException(code, message, status)
        {
            RequestId = requestId,
            TraceId = body?.TraceId ?? Header(response, HeaderNames.TraceId),
            Errors = body?.Errors?.ToArray() ?? new LabelFieldError[0],
        };
    }

    /// <summary>
    /// 没有错误响应体时（如经过代理）按 HTTP 状态码推断错误码。
    /// </summary>
    /// <param name="status">HTTP 状态码。</param>
    /// <returns>错误码；认不出时为 UNEXPECTED_RESPONSE。</returns>
    private static string CodeFromStatus(int status)
    {
        switch (status)
        {
            case 401:
                return LabelErrorCodes.Unauthorized;
            case 403:
                return LabelErrorCodes.TemplateForbidden;
            case 404:
                return LabelErrorCodes.TemplateNotFound;
            case 413:
                return LabelErrorCodes.BatchTooLarge;
            case 500:
                return LabelErrorCodes.RenderFailed;
            default:
                return LabelErrorCodes.UnexpectedResponse;
        }
    }

    /// <summary>
    /// 触发 <see cref="CallCompleted"/>；处理函数抛出的异常只写跟踪日志，不影响调用结果。
    /// </summary>
    /// <param name="info">这次调用的记录。</param>
    private void RaiseCallCompleted(CallCompletedEventArgs info)
    {
        try
        {
            CallCompleted?.Invoke(this, info);
        }
        catch (Exception ex)
        {
            // 日志处理函数出错不能影响调用结果。
            Trace.TraceWarning("LabelService.Client CallCompleted 处理函数出错：" + ex.Message);
        }
    }

    /// <summary>
    /// 取响应头的值，多个值用逗号连起来。
    /// </summary>
    /// <param name="response">响应。</param>
    /// <param name="name">响应头名称。</param>
    /// <returns>响应头的值；没有这个头时为 null。</returns>
    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    /// <summary>
    /// 按不变区域性解析整数。
    /// </summary>
    /// <param name="value">文本；可以为 null。</param>
    /// <returns>解析出的整数；为 null 或格式不对时为 null。</returns>
    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (int?)null;

    /// <summary>
    /// Server-Timing 里的 total 阶段，即服务端处理耗时。
    /// </summary>
    /// <param name="timing">解析好的“阶段 → 毫秒”。</param>
    /// <returns>服务端耗时；没有 total 时为 null。</returns>
    private static TimeSpan? ServerDuration(Dictionary<string, double> timing) =>
        timing.TryGetValue("total", out var total) ? TimeSpan.FromTicks((long)(total * TimeSpan.TicksPerMillisecond)) : (TimeSpan?)null;

    /// <summary>
    /// 默认的消息处理器：.NET 8 以上用定期重建连接的 SocketsHttpHandler，旧框架用 HttpClientHandler。
    /// </summary>
    /// <returns>消息处理器，由 HttpClient 负责释放。</returns>
    private static HttpMessageHandler CreateDefaultHandler()
    {
#if NET8_0_OR_GREATER
        // 定期重建连接，DNS 切换后不会一直连旧地址。
        return new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
#else
        return new HttpClientHandler();
#endif
    }
}
