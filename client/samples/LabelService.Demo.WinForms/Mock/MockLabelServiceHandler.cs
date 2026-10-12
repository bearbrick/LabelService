using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace LabelService.Demo.Mock;

/// <summary>
/// 内置模拟服务（设计书第 11 节）：不连服务器也能演示 SDK。按开放接口的路径、请求体、响应头和错误码返回，
/// 标签图是用 System.Drawing 画的示意图（不是真实的渲染引擎）。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>两个模板：BOX_LABEL（成品箱标）、A4_SHIPPING_MARK（出口唛头）；别的编码返回 404 TEMPLATE_NOT_FOUND。</item>
/// <item>API Key 要以 lbl_ 开头，否则 401 UNAUTHORIZED。</item>
/// <item>必填字段为空返回 400 FIELD_REQUIRED，逐条列出。</item>
/// <item>“模拟网络抖动”打开时，每个调用（同一个 X-Request-Id）第一次返回 503，SDK 重试后成功。</item>
/// </list>
/// </remarks>
internal sealed class MockLabelServiceHandler : HttpMessageHandler
{
    /// <summary>
    /// 响应体的 JSON 设置：驼峰命名，中文不转义（和服务端一致）。
    /// </summary>
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>
    /// 已经故意失败过一次的 X-Request-Id（模拟网络抖动用）。
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> failedOnce = new ConcurrentDictionary<string, bool>();

    /// <summary>
    /// 模拟网络延迟的随机数。
    /// </summary>
    private readonly Random random = new Random();

    /// <summary>
    /// 每个调用第一次返回 503。
    /// </summary>
    public bool Flaky
    {
        get; set;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        await Task.Delay(random.Next(15, 45), cancellationToken).ConfigureAwait(false);
        var requestId = request.Headers.TryGetValues("X-Request-Id", out var ids) ? ids.First() : Guid.NewGuid().ToString("N");
        // 模拟网络抖动：同一个调用（X-Request-Id）第一次返回 503，SDK 重试时带的是同一个 ID，第二次就成功。
        if (Flaky && failedOnce.TryAdd(requestId, true))
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("Service Unavailable") };
        }

        var key = request.Headers.TryGetValues("X-Api-Key", out var keys) ? keys.First() : "";
        if (!key.StartsWith("lbl_", StringComparison.Ordinal))
        {
            return Error(HttpStatusCode.Unauthorized, "UNAUTHORIZED", "API Key 缺失或无效");
        }

        // 按路径分发：模板列表、字段定义、单张生成、批量生成。
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path == "/api/v1/templates")
        {
            return JsonResponse(HttpStatusCode.OK, MockTemplates.List());
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/templates/", StringComparison.Ordinal) && path.EndsWith("/schema", StringComparison.Ordinal))
        {
            var code = Uri.UnescapeDataString(path.Substring("/api/v1/templates/".Length, path.Length - "/api/v1/templates/".Length - "/schema".Length));
            var template = MockTemplates.Find(code);
            return template is null ? NotFound(code) : JsonResponse(HttpStatusCode.OK, template.Schema());
        }

        if (request.Method == HttpMethod.Post && (path == "/api/v1/render" || path == "/api/v1/render/batch"))
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync().ConfigureAwait(false));

            // A4 唛头这样的大模板用来演示“慢调用”：固定多等 110 ms，保证服务端耗时超过 100 ms，不看机器快慢
            if (MockTemplates.Find(Text(body.RootElement, "templateCode")) is { Width: > 150 })
            {
                await Task.Delay(110, cancellationToken).ConfigureAwait(false);
            }

            return path.EndsWith("/batch", StringComparison.Ordinal) ? RenderBatch(body.RootElement, started) : RenderSingle(body.RootElement, started);
        }

        return Error(HttpStatusCode.NotFound, "TEMPLATE_NOT_FOUND", "模拟服务没有这个接口：" + path);
    }

    /// <summary>
    /// 单张生成：找模板、校验必填字段、按格式编码。
    /// </summary>
    /// <param name="body">请求体。</param>
    /// <param name="started">开始处理的时间戳（算 Server-Timing）。</param>
    /// <returns>文件响应或错误响应。</returns>
    private HttpResponseMessage RenderSingle(JsonElement body, long started)
    {
        var code = body.TryGetProperty("templateCode", out var c) ? c.GetString() ?? "" : "";
        var template = MockTemplates.Find(code);
        if (template is null)
        {
            return NotFound(code);
        }

        var format = Text(body, "format");
        var dpi = body.TryGetProperty("dpi", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : template.Dpi;
        var copies = body.TryGetProperty("copies", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 1;
        var data = Data(body, "data");
        var missing = template.Missing(data, null);
        if (missing.Count > 0)
        {
            return Error(HttpStatusCode.BadRequest, "FIELD_REQUIRED", "缺少必填字段 " + missing[0]["field"], missing);
        }

        var (content, contentType) = Encode(template, format, dpi, new[] { (data, copies) });
        return File(content, contentType, template, format, dpi, copies, started);
    }

    /// <summary>
    /// 批量生成：每条合并公共字段后校验，全部通过才生成；ZIP 时每条一个文件，否则合并成一个文件。
    /// </summary>
    /// <param name="body">请求体。</param>
    /// <param name="started">开始处理的时间戳（算 Server-Timing）。</param>
    /// <returns>文件响应或错误响应。</returns>
    private HttpResponseMessage RenderBatch(JsonElement body, long started)
    {
        var code = Text(body, "templateCode");
        var template = MockTemplates.Find(code);
        if (template is null)
        {
            return NotFound(code);
        }

        var format = Text(body, "format");
        var dpi = body.TryGetProperty("dpi", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : template.Dpi;
        var common = Data(body, "common");
        var labels = new List<(Dictionary<string, string> Data, int Copies)>();
        var errors = new List<Dictionary<string, object>>();
        var index = 0;
        // 每条合并公共字段后校验必填，错误明细带条目序号，全部通过才生成。
        foreach (var item in body.GetProperty("items").EnumerateArray())
        {
            var data = new Dictionary<string, string>(common, StringComparer.Ordinal);
            foreach (var pair in Data(item, "data"))
            {
                data[pair.Key] = pair.Value;
            }

            errors.AddRange(template.Missing(data, index++));
            labels.Add((data, item.TryGetProperty("copies", out var n) ? n.GetInt32() : 1));
        }

        if (errors.Count > 0)
        {
            return Error(HttpStatusCode.BadRequest, "FIELD_REQUIRED", $"第 {(int)errors[0]["index"] + 1} 条缺少必填字段 {errors[0]["field"]}", errors);
        }

        var total = labels.Sum(label => label.Copies);
        if (Text(body, "output") == "zip")
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (var i = 0; i < labels.Count; i++)
                {
                    var (content, _) = Encode(template, format, dpi, new[] { (labels[i].Data, format == "png" ? 1 : labels[i].Copies) });
                    using var entry = zip.CreateEntry($"{template.Code}_{i + 1:D4}.{format}").Open();
                    entry.Write(content, 0, content.Length);
                }
            }

            return File(stream.ToArray(), "application/zip", template, "zip", dpi, total, started);
        }

        var (merged, contentType) = Encode(template, format, dpi, labels);
        return File(merged, contentType, template, format, dpi, total, started);
    }

    /// <summary>
    /// 按格式生成文件；PNG 只画第一张。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="format">pdf、png 或 zpl。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="labels">每张标签的字段值和份数。</param>
    /// <returns>文件内容和内容类型。</returns>
    private static (byte[] Content, string ContentType) Encode(MockTemplate template, string format, int dpi, IEnumerable<(Dictionary<string, string> Data, int Copies)> labels)
    {
        switch (format)
        {
            case "pdf":
                return (MockDocuments.Pdf(template, labels), "application/pdf");
            case "zpl":
                return (MockDocuments.Zpl(template, dpi, labels), "application/zpl; charset=utf-8");
            default:
                return (MockDocuments.Png(template, dpi, labels.First().Data), "image/png");
        }
    }

    /// <summary>
    /// 成功的文件响应：文件名、版本、张数、DPI、跟踪 ID 和按实际耗时拆分的 Server-Timing。
    /// </summary>
    /// <param name="content">文件内容。</param>
    /// <param name="contentType">内容类型。</param>
    /// <param name="template">模板。</param>
    /// <param name="extension">文件扩展名。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="count">张数。</param>
    /// <param name="started">开始处理的时间戳。</param>
    /// <returns>响应。</returns>
    private static HttpResponseMessage File(byte[] content, string contentType, MockTemplate template, string extension, int dpi, int count, long started)
    {
        var total = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = $"{template.Code}_{DateTime.Today:yyyyMMdd}.{extension}",
        };
        response.Headers.Add("X-Template-Version", template.Version.ToString(CultureInfo.InvariantCulture));
        response.Headers.Add("X-Label-Count", count.ToString(CultureInfo.InvariantCulture));
        response.Headers.Add("X-Label-Dpi", dpi.ToString(CultureInfo.InvariantCulture));
        response.Headers.Add("X-Trace-Id", TraceId());
        response.Headers.Add("Server-Timing", string.Format(CultureInfo.InvariantCulture,
            "auth;dur=0.1, tpl;dur=0.1, validate;dur=0.2, layout;dur={0:0.0}, draw;dur={1:0.0}, encode;dur={2:0.0}, total;dur={3:0.0}",
            total * 0.1, total * 0.3, total * 0.5, total));
        return response;
    }

    /// <summary>
    /// 模板不存在的 404 响应。
    /// </summary>
    /// <param name="code">请求的模板编码。</param>
    /// <returns>响应。</returns>
    private static HttpResponseMessage NotFound(string code) =>
        Error(HttpStatusCode.NotFound, "TEMPLATE_NOT_FOUND", $"模板 {code} 不存在、版本不存在或还没有发布（模拟服务只有 BOX_LABEL、A4_SHIPPING_MARK）");

    /// <summary>
    /// 错误响应：和服务端一样的错误体（code、message、errors、traceId）和 X-Trace-Id 头。
    /// </summary>
    /// <param name="status">状态码。</param>
    /// <param name="code">错误码。</param>
    /// <param name="message">错误说明。</param>
    /// <param name="errors">逐条错误；没有时为 null。</param>
    /// <returns>响应。</returns>
    private static HttpResponseMessage Error(HttpStatusCode status, string code, string message, IEnumerable<Dictionary<string, object>>? errors = null)
    {
        var traceId = TraceId();
        var response = JsonResponse(status, new
        {
            code,
            message,
            errors,
            traceId
        });
        response.Headers.Add("X-Trace-Id", traceId);
        return response;
    }

    /// <summary>
    /// JSON 响应。
    /// </summary>
    /// <param name="status">状态码。</param>
    /// <param name="value">响应体对象。</param>
    /// <returns>响应。</returns>
    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object value) =>
        new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json") };

    /// <summary>
    /// 随机的 W3C traceparent 格式跟踪 ID。
    /// </summary>
    /// <returns>跟踪 ID。</returns>
    private static string TraceId() => "00-" + Guid.NewGuid().ToString("N") + "-" + Guid.NewGuid().ToString("N").Substring(0, 16) + "-01";

    /// <summary>
    /// 取请求体里的字符串属性。
    /// </summary>
    /// <param name="body">请求体。</param>
    /// <param name="name">属性名。</param>
    /// <returns>属性值；没有或不是字符串时为空字符串。</returns>
    private static string Text(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    /// <summary>
    /// 取请求体里的字段值对象，值都转成文本（字符串取原文，其他取 JSON 写法）。
    /// </summary>
    /// <param name="body">请求体或批量里的一条。</param>
    /// <param name="name">属性名，如 data、common。</param>
    /// <returns>字段 key → 文本；没有时为空。</returns>
    private static Dictionary<string, string> Data(JsonElement body, string name)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        if (body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                data[property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? "" : property.Value.GetRawText();
            }
        }

        return data;
    }
}
