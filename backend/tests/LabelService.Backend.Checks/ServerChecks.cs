using System.Net.Http.Json;
using System.Text;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;

namespace LabelService.Backend.Checks;

/// <summary>
/// 服务端：在进程内启动真实服务（开发环境配置和示例模板），走 HTTP 检查开放接口。
/// SDK 对真实服务的端到端检查在 client/tests/LabelService.Client.Checks（由根目录 verify.ps1 启动服务后运行）。
/// </summary>
internal static class ServerChecks
{
    /// <summary>
    /// 运行开放接口的全部检查，结束后删除临时数据目录。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await RunAsync(backendRoot, dataDirectory, check);
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    /// <summary>
    /// 启动服务并逐项检查开放接口：健康检查、单张生成和响应头、鉴权、请求错误码、字段定义。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="dataDirectory">临时数据目录。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task RunAsync(string backendRoot, string dataDirectory, Action<string, bool> check)
    {
        await using var factory = new ServerFactory(backendRoot, dataDirectory);
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
        check("示例模板用打包的字体渲染，没有字体降级警告", Header(ok, HeaderNames.LabelWarnings)?.Contains(WarningCodes.FontFallback) != true);
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

        var listed = await ListTemplatesAsync(http, Fixtures.DevApiKey);
        var box = listed.Response?.Templates.FirstOrDefault(item => item.TemplateCode == "BOX_LABEL");
        check("模板列表接口列出已发布的示例模板，按编码排序，带名称、版本和尺寸",
            listed.Status == 200 && box is { Name: "成品箱标", Version: 1, Page.Width: 100, Page.Height: 60 }
            && listed.Response!.Templates.Any(item => item.TemplateCode == "A4_SHIPPING_MARK")
            && listed.Response.Templates.Select(item => item.TemplateCode).SequenceEqual(listed.Response.Templates.Select(item => item.TemplateCode).Order(StringComparer.Ordinal)));
        check("模板列表接口没有 Key 返回 401", (await ListTemplatesAsync(http, null)).Status == 401);

    }

    /// <summary>
    /// 调单张生成接口。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="json">请求体。</param>
    /// <param name="apiKey">API Key，默认开发 Key；null 表示不带 Key。</param>
    /// <returns>响应。</returns>
    internal static async Task<HttpResponseMessage> Post(HttpClient http, string json, string? apiKey = Fixtures.DevApiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (apiKey is not null)
        {
            request.Headers.Add(HeaderNames.ApiKey, apiKey);
        }

        return await http.SendAsync(request);
    }

    /// <summary>
    /// 调模板列表接口。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="apiKey">API Key；null 表示不带 Key。</param>
    /// <returns>状态码和成功时的响应体（失败时为 null）。</returns>
    internal static async Task<(int Status, TemplateListResponse? Response)> ListTemplatesAsync(HttpClient http, string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/templates");
        if (apiKey is not null)
        {
            request.Headers.Add(HeaderNames.ApiKey, apiKey);
        }

        using var response = await http.SendAsync(request);
        return ((int)response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TemplateListResponse>(LabelJson.Options) : null);
    }

    /// <summary>
    /// 响应的状态码和错误响应里的错误码。
    /// </summary>
    /// <param name="response">响应。</param>
    /// <returns>状态码和错误码（没有错误体时为 null）。</returns>
    private static async Task<(int Status, string? Code)> Code(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(LabelJson.Options);
        return ((int)response.StatusCode, body?.Code);
    }

    /// <summary>
    /// 取响应头的值，多个值用逗号连起来。
    /// </summary>
    /// <param name="response">响应。</param>
    /// <param name="name">响应头名称。</param>
    /// <returns>响应头的值；没有这个头时为 null。</returns>
    internal static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
