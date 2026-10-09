using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LabelService.Client;

namespace LabelService.Client.Checks;

/// <summary>
/// 客户端 SDK：请求头、请求体、响应头解析、重试、错误映射、本地预校验。服务端用假的 HttpMessageHandler 代替。
/// </summary>
internal static class ClientChecks
{
    public static async Task RunAsync(Action<string, bool> check)
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(PngResponse()));
        using (var client = Client(handler))
        {
            CallCompletedEventArgs? completed = null;
            client.CallCompleted += (_, e) => completed = e;
            var file = await client.RenderAsync(new RenderRequest("BOX_LABEL", LabelFormat.Png)
                .Set("prodDate", new DateTime(2026, 10, 8))
                .Set("logo", new byte[] { 1, 2, 3 })
                .Set("qty", 50)
                .Set("skip", null));
            var (request, body) = handler.Requests[0];
            check("SDK 带上 X-Api-Key、X-Request-Id 和 User-Agent",
                request.Headers.GetValues("X-Api-Key").Single() == "lbl_test_checks"
                && Guid.TryParse(request.Headers.GetValues("X-Request-Id").Single(), out _)
                && request.Headers.UserAgent.ToString().StartsWith("LabelService.Client/", StringComparison.Ordinal));
            check("SDK 请求体：格式小写、日期转 yyyy-MM-dd、byte[] 转 base64、null 不发送",
                body!.Contains("\"format\":\"png\"") && body.Contains("\"prodDate\":\"2026-10-08\"")
                && body.Contains("\"logo\":\"AQID\"") && body.Contains("\"qty\":50") && !body.Contains("skip"));
            check("SDK 解析响应头",
                file is { TemplateVersion: 3, LabelCount: 1, Dpi: 300, TraceId: "00-abc", FileName: "BOX_LABEL_20261008.png" }
                && file.Warnings.SequenceEqual(["FONT_FALLBACK:SimSun"]));
            check("SDK 解析服务端耗时", Math.Abs(file.ServerDuration!.Value.TotalMilliseconds - 46.1) < 0.01 && file.ServerTiming["auth"] == 0.3);
            check("CallCompleted 记录成功调用", completed is { ErrorCode: null, Attempts: 1, StatusCode: 200 } && completed.RequestId == file.RequestId);
            check("同步方法可用", client.Render(new RenderRequest("BOX_LABEL", LabelFormat.Png)).Content.Length == 4);
        }

        handler = new FakeHandler((_, attempt, _) => Task.FromResult(attempt == 1 ? Status(HttpStatusCode.ServiceUnavailable) : PngResponse()));
        using (var client = Client(handler))
        {
            CallCompletedEventArgs? completed = null;
            client.CallCompleted += (_, e) => completed = e;
            await client.RenderAsync(new RenderRequest("BOX_LABEL", LabelFormat.Png));
            check("503 自动重试后成功", handler.Requests.Count == 2 && completed is { Attempts: 2, ErrorCode: null });
        }

        handler = new FakeHandler((_, _, _) => Task.FromResult(Status(HttpStatusCode.BadGateway, "<html>bad gateway</html>", "text/html")));
        var gateway = await Fails(handler);
        check("网关一直返回 502：重试 2 次后抛 SERVICE_UNAVAILABLE", gateway is { ErrorCode: LabelErrorCodes.ServiceUnavailable, IsTransient: true } && handler.Requests.Count == 3);

        const string fieldError = """{"code":"FIELD_REQUIRED","message":"第 2 条缺少必填字段 boxNo","errors":[{"index":1,"field":"boxNo","code":"FIELD_REQUIRED"}],"traceId":"00-def"}""";
        handler = new FakeHandler((_, _, _) => Task.FromResult(Status(HttpStatusCode.BadRequest, fieldError)));
        var required = await Fails(handler);
        check("400 不重试，错误明细映射到 Errors",
            required is { ErrorCode: LabelErrorCodes.FieldRequired, StatusCode: 400, TraceId: "00-def", IsTransient: false }
            && required.Errors is [{ Index: 1, Field: "boxNo" }] && handler.Requests.Count == 1);

        handler = new FakeHandler((_, _, _) => throw new HttpRequestException("连接被拒绝"));
        check("网络错误映射为 NETWORK_ERROR 并重试", await Fails(handler) is { ErrorCode: LabelErrorCodes.NetworkError } && handler.Requests.Count == 3);

        handler = new FakeHandler(async (_, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return PngResponse();
        });
        check("超时映射为 TIMEOUT", await Fails(handler, new LabelServiceClientOptions { Timeout = TimeSpan.FromMilliseconds(100), MaxRetries = 0 }) is { ErrorCode: LabelErrorCodes.Timeout });

        handler = new FakeHandler((_, _, _) => Task.FromResult(Status((HttpStatusCode)418, "<html>teapot</html>", "text/html")));
        check("无法识别的响应映射为 UNEXPECTED_RESPONSE", await Fails(handler) is { ErrorCode: LabelErrorCodes.UnexpectedResponse, StatusCode: 418 });

        handler = new FakeHandler((_, _, _) => Task.FromResult(PngResponse()));
        using (var client = Client(handler))
        {
            check("本地预校验：模板编码为空", ThrowsArgument(() => client.RenderAsync(new RenderRequest(" ", LabelFormat.Pdf))));
            check("本地预校验：份数超出 1–100", ThrowsArgument(() => client.RenderAsync(new RenderRequest("A", LabelFormat.Pdf) { Copies = 101 })));
            check("本地预校验：PNG 单张只能 1 份", ThrowsArgument(() => client.RenderAsync(new RenderRequest("A", LabelFormat.Png) { Copies = 2 })));
            var pngMerge = new BatchRenderRequest("A", LabelFormat.Png) { Output = BatchOutput.Merge };
            pngMerge.AddItem();
            check("本地预校验：PNG 批量必须 Zip", ThrowsArgument(() => client.RenderBatchAsync(pngMerge)));
            var tooMany = new BatchRenderRequest("A", LabelFormat.Zpl);
            for (var i = 0; i < 6; i++)
            {
                tooMany.AddItem(copies: 100);
            }

            check("本地预校验：批量总张数超过 500", ThrowsArgument(() => client.RenderBatchAsync(tooMany)));
            check("本地预校验不发请求", handler.Requests.Count == 0);

            var batch = new BatchRenderRequest("BOX_LABEL", LabelFormat.Pdf).SetCommon("batchNo", "20261008-A");
            batch.AddItem().Set("boxNo", "C1");
            batch.AddItem(copies: 2).Set("boxNo", "C2");
            await client.RenderBatchAsync(batch);
            var batchBody = handler.Requests[0].Body!;
            check("批量请求体：output、common、items 和份数",
                handler.Requests[0].Request.RequestUri!.AbsolutePath == "/api/v1/render/batch"
                && batchBody.Contains("\"output\":\"merge\"") && batchBody.Contains("\"common\":{\"batchNo\":\"20261008-A\"}")
                && batchBody.Contains("{\"data\":{\"boxNo\":\"C2\"},\"copies\":2}"));
        }
    }

    private static LabelServiceClient Client(HttpMessageHandler handler, LabelServiceClientOptions? options = null)
    {
        options ??= new LabelServiceClientOptions();
        options.BaseAddress = new Uri("http://label.test");
        options.ApiKey = "lbl_test_checks";
        options.RetryDelay = TimeSpan.FromMilliseconds(5);
        return new LabelServiceClient(options, handler);
    }

    private static async Task<LabelServiceException?> Fails(HttpMessageHandler handler, LabelServiceClientOptions? options = null)
    {
        using var client = Client(handler, options);
        try
        {
            await client.RenderAsync(new RenderRequest("BOX_LABEL", LabelFormat.Png));
            return null;
        }
        catch (LabelServiceException ex)
        {
            return ex;
        }
    }

    private static bool ThrowsArgument(Func<Task> action)
    {
        try
        {
            action().GetAwaiter().GetResult();
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static HttpResponseMessage PngResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "BOX_LABEL_20261008.png" };
        response.Headers.Add("X-Template-Version", "3");
        response.Headers.Add("X-Label-Count", "1");
        response.Headers.Add("X-Label-Dpi", "300");
        response.Headers.Add("X-Trace-Id", "00-abc");
        response.Headers.Add("Server-Timing", "auth;dur=0.3, total;dur=46.1");
        response.Headers.Add("X-Label-Warnings", "FONT_FALLBACK:SimSun");
        return response;
    }

    private static HttpResponseMessage Status(HttpStatusCode status, string body = "", string mediaType = "application/json") =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        };

    /// <summary>
    /// 记录请求并按尝试次数返回预设响应的假处理器。
    /// </summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return await respond(request, Requests.Count, cancellationToken);
        }
    }
}
