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
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly ConcurrentDictionary<string, bool> failedOnce = new ConcurrentDictionary<string, bool>();
    private readonly Random random = new Random();

    /// <summary>每个调用第一次返回 503。</summary>
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
        if (Flaky && failedOnce.TryAdd(requestId, true))
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("Service Unavailable") };
        }

        var key = request.Headers.TryGetValues("X-Api-Key", out var keys) ? keys.First() : "";
        if (!key.StartsWith("lbl_", StringComparison.Ordinal))
        {
            return Error(HttpStatusCode.Unauthorized, "UNAUTHORIZED", "API Key 缺失或无效");
        }

        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/templates/", StringComparison.Ordinal) && path.EndsWith("/schema", StringComparison.Ordinal))
        {
            var code = Uri.UnescapeDataString(path.Substring("/api/v1/templates/".Length, path.Length - "/api/v1/templates/".Length - "/schema".Length));
            var template = MockTemplates.Find(code);
            return template is null ? NotFound(code) : JsonResponse(HttpStatusCode.OK, template.Schema());
        }

        if (request.Method == HttpMethod.Post && (path == "/api/v1/render" || path == "/api/v1/render/batch"))
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync().ConfigureAwait(false));
            return path.EndsWith("/batch", StringComparison.Ordinal) ? RenderBatch(body.RootElement, started) : RenderSingle(body.RootElement, started);
        }

        return Error(HttpStatusCode.NotFound, "TEMPLATE_NOT_FOUND", "模拟服务没有这个接口：" + path);
    }

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

    private static HttpResponseMessage NotFound(string code) =>
        Error(HttpStatusCode.NotFound, "TEMPLATE_NOT_FOUND", $"模板 {code} 不存在、版本不存在或还没有发布（模拟服务只有 BOX_LABEL、A4_SHIPPING_MARK）");

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

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object value) =>
        new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json") };

    private static string TraceId() => "00-" + Guid.NewGuid().ToString("N") + "-" + Guid.NewGuid().ToString("N").Substring(0, 16) + "-01";

    private static string Text(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

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
