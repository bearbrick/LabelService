using System.Net.Http.Json;
using System.Text;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LabelService.Backend.Checks;

/// <summary>
/// 服务端：在进程内启动真实服务（开发环境配置和示例模板），走 HTTP 检查开放接口。
/// SDK 对真实服务的端到端检查在 client/tests/LabelService.Client.Checks（由根目录 verify.ps1 启动服务后运行）。
/// </summary>
internal static class ServerChecks
{
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        await using var factory = new ServerFactory(Path.Combine(backendRoot, "src", "LabelService.Server"));
        using var http = factory.CreateClient();

        var health = await http.GetAsync("/health");
        check("/health 返回 200", health.IsSuccessStatusCode);

        var ok = await Post(http, $$"""{"templateCode":"BOX_LABEL","format":"png","data":{{Fixtures.BoxLabelData}}}""");
        var png = await ok.Content.ReadAsByteArrayAsync();
        check("单张 PNG 返回 200 和 PNG 文件", ok.IsSuccessStatusCode && ok.Content.Headers.ContentType?.MediaType == "image/png" && png.AsSpan().StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47]));
        check("成功响应带版本、张数、DPI、链路 ID",
            Header(ok, HeaderNames.TemplateVersion) == "1" && Header(ok, HeaderNames.LabelCount) == "1"
            && Header(ok, HeaderNames.LabelDpi) == "300" && Header(ok, HeaderNames.TraceId) is { Length: > 0 });
        var stages = Header(ok, HeaderNames.ServerTiming)!.Split(", ").Select(item => item.Split(';')[0]).ToArray();
        check("Server-Timing 包含全部阶段且按规范顺序", stages.SequenceEqual(TimingStages.All));
        check("文件名是 模板编码_日期.png", ok.Content.Headers.ContentDisposition?.FileName?.Trim('"') is { } name && name.StartsWith("BOX_LABEL_") && name.EndsWith(".png"));

        var unauthorized = await Post(http, "{}", apiKey: null);
        check("没有 Key 返回 401 UNAUTHORIZED，且带 Server-Timing",
            await Code(unauthorized) == (401, ErrorCodes.Unauthorized) && Header(unauthorized, HeaderNames.ServerTiming) is not null);
        check("非法 JSON 返回 INVALID_JSON", await Code(await Post(http, "{bad")) == (400, ErrorCodes.InvalidJson));
        check("缺少 templateCode 返回 INVALID_JSON", await Code(await Post(http, """{"format":"png","data":{}}""")) == (400, ErrorCodes.InvalidJson));
        check("format 非法返回 FORMAT_INVALID", await Code(await Post(http, """{"templateCode":"BOX_LABEL","format":"bmp","data":{}}""")) == (400, ErrorCodes.FormatInvalid));
        check("dpi 非法返回 DPI_INVALID", await Code(await Post(http, """{"templateCode":"BOX_LABEL","format":"png","dpi":250,"data":{}}""")) == (400, ErrorCodes.DpiInvalid));
        check("copies 超范围返回 COPIES_INVALID", await Code(await Post(http, """{"templateCode":"BOX_LABEL","format":"zpl","copies":101,"data":{}}""")) == (400, ErrorCodes.CopiesInvalid));
        check("PNG 多份返回 COPIES_INVALID", await Code(await Post(http, """{"templateCode":"BOX_LABEL","format":"png","copies":2,"data":{}}""")) == (400, ErrorCodes.CopiesInvalid));
        check("模板不存在返回 404 TEMPLATE_NOT_FOUND", await Code(await Post(http, """{"templateCode":"NOPE","format":"png","data":{}}""")) == (404, ErrorCodes.TemplateNotFound));
        var missing = await (await Post(http, """{"templateCode":"BOX_LABEL","format":"png","data":{"prodDate":"2026-10-08"}}""")).Content.ReadFromJsonAsync<ErrorResponse>(LabelJson.Options);
        check("缺字段返回 FIELD_REQUIRED 并列出全部缺失字段",
            missing is { Code: ErrorCodes.FieldRequired, Errors.Count: 5, TraceId.Length: > 0 });
        var pdf = await Post(http, $$"""{"templateCode":"BOX_LABEL","format":"pdf","data":{{Fixtures.BoxLabelData}}}""");
        check("PDF：已实现时返回 200，未实现时返回 501 NOT_IMPLEMENTED",
            new LabelRenderer().Supports(LabelFormat.Pdf) ? pdf.IsSuccessStatusCode : await Code(pdf) == (501, "NOT_IMPLEMENTED"));

        using var schemaRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/templates/BOX_LABEL/schema");
        schemaRequest.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        var schema = await (await http.SendAsync(schemaRequest)).Content.ReadFromJsonAsync<TemplateSchemaResponse>(LabelJson.Options);
        check("字段定义接口返回字段和样例，字段里不重复带 sample",
            schema is { TemplateCode: "BOX_LABEL", Version: 1, Fields.Count: 6, Sample.Count: 6 } && schema.Fields.All(field => field.Sample is null));

    }

    private static async Task<HttpResponseMessage> Post(HttpClient http, string json, string? apiKey = Fixtures.DevApiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (apiKey is not null)
        {
            request.Headers.Add(HeaderNames.ApiKey, apiKey);
        }

        return await http.SendAsync(request);
    }

    private static async Task<(int Status, string? Code)> Code(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(LabelJson.Options);
        return ((int)response.StatusCode, body?.Code);
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    /// <summary>
    /// 指定内容根目录（WebApplicationFactory 默认按 .sln 推断，不认识 .slnx），并只输出警告以上的日志。
    /// </summary>
    private sealed class ServerFactory(string contentRoot) : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseContentRoot(contentRoot).ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Warning", ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning" }));
    }
}
