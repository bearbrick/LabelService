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
    private readonly LabelServiceClientOptions options;
    private readonly HttpClient http;
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
        return SendAsync("GetSchema", templateCode, HttpMethod.Get, path, null, ReadSchemaAsync, cancellationToken);
    }

    /// <inheritdoc />
    public TemplateSchema GetSchema(string templateCode) => Task.Run(() => GetSchemaAsync(templateCode)).GetAwaiter().GetResult();

    /// <summary>
    /// 释放内部的 HttpClient。
    /// </summary>
    public void Dispose() => http.Dispose();

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

    private static async Task<TemplateSchema> ReadSchemaAsync(HttpResponseMessage response, string requestId)
    {
        var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<TemplateSchema>(content, ClientJson.Options)
                ?? throw new JsonException("响应体为空");
        }
        catch (JsonException ex)
        {
            throw new LabelServiceException(LabelErrorCodes.UnexpectedResponse, "无法解析字段定义：" + ex.Message, (int)response.StatusCode, ex)
            {
                RequestId = requestId,
                TraceId = Header(response, HeaderNames.TraceId),
            };
        }
    }

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

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (int?)null;

    private static TimeSpan? ServerDuration(Dictionary<string, double> timing) =>
        timing.TryGetValue("total", out var total) ? TimeSpan.FromTicks((long)(total * TimeSpan.TicksPerMillisecond)) : (TimeSpan?)null;

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
