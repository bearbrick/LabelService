using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using LabelService.Contracts.Protocol;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// 服务端预览和字体下载：未保存的模板直接渲染、错误位置、和开放接口鉴权隔离、指标单独维度；
/// /fonts 提供和渲染同一份字体文件。
/// </summary>
internal static class PreviewChecks
{
    /// <summary>
    /// 运行预览和字体下载的全部检查。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="admin">已登录并改过密码的 admin 会话。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(string backendRoot, HttpClient http, AdminSession admin, Action<string, bool> check)
    {
        CheckFontSources(backendRoot, check);
        await CheckPreviewAsync(backendRoot, http, admin, check);
        await CheckFontFilesAsync(http, check);
    }

    /// <summary>
    /// 字体注册表能按模板字体名查到字体文件（认别名、可变字体有标记），不认识的名字返回 null。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    private static void CheckFontSources(string backendRoot, Action<string, bool> check)
    {
        var fonts = Fixtures.Fonts(backendRoot);
        var serif = fonts.SourceOf("思源宋体");
        check("字体注册表能查到模板字体名对应的文件（认别名，可变字体有标记），不认识的名字不降级",
            serif is { IsVariable: true } && Path.GetFileName(serif.Path).Contains("Serif", StringComparison.Ordinal)
            && fonts.SourceOf("SourceHanSansSC")?.Path != serif.Path && fonts.SourceOf("Arial") is null);
    }

    /// <summary>
    /// 预览接口：未保存的模板直接渲染，报错时带元素位置，和开放接口的鉴权互不通用，指标单独记。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="admin">admin 会话。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckPreviewAsync(string backendRoot, HttpClient http, AdminSession admin, Action<string, bool> check)
    {
        var template = JsonNode.Parse(File.ReadAllText(Path.Combine(backendRoot, "samples", "templates", "box-label.json")))!.AsObject();
        template["code"] = "UNSAVED_PREVIEW";
        var stages = 0;
        var previewRequests = 0;
        byte[] sample;
        using (var listener = new MeterListener())
        {
            listener.InstrumentPublished = (instrument, meters) =>
            {
                if (instrument.Meter.Name == "LabelService")
                {
                    meters.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            {
                if (instrument.Name == "label_render_stage_ms")
                {
                    Interlocked.Increment(ref stages);
                }
            });
            listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (instrument.Name == "label_requests_total" && tag is { Key: "endpoint", Value: "preview" })
                    {
                        Interlocked.Increment(ref previewRequests);
                    }
                }
            });
            listener.Start();
            var response = await admin.PostAsync("/api/admin/preview", new
            {
                template,
                dpi = 300
            });
            sample = await response.Content.ReadAsByteArrayAsync();
            using var image = SKBitmap.Decode(sample);
            check("预览未保存的模板：返回 PNG，尺寸按请求的 DPI（100 mm @ 300 dpi = 1181 点），带 DPI 和分阶段耗时头",
                response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentType?.MediaType == "image/png"
                && image is { Width: 1181 } && response.Headers.GetValues(HeaderNames.LabelDpi).Single() == "300"
                && response.Headers.Contains(HeaderNames.ServerTiming));
        }

        check("预览计入 endpoint=preview 的请求数，不计入阶段耗时（开放接口的指标不受影响）", previewRequests == 1 && stages == 0);

        var custom = await admin.PostAsync("/api/admin/preview", new
        {
            template,
            dpi = 300,
            data = new
            {
                partName = "另一个零件名称",
                partNo = "9",
                qty = 1,
                batchNo = "B",
                prodDate = "2026-10-09",
                boxNo = "C9"
            }
        });
        check("预览可以传数据，不传时用样例值", custom.IsSuccessStatusCode && !(await custom.Content.ReadAsByteArrayAsync()).AsSpan().SequenceEqual(sample));

        const string broken = """{"template":{"schemaVersion":"1.0","code":"X","name":"x","page":{"width":50,"height":30},"fields":[],"elements":[{"type":"circle","id":"e1","x":0,"y":0,"width":5,"height":5}]}}""";
        var invalid = await admin.PostAsync("/api/admin/preview", new StringContent(broken, Encoding.UTF8, "application/json"));
        var message = (await AdminSession.ReadAsync(invalid)).GetProperty("message").GetString() ?? "";
        check("模板 JSON 不合法时返回 400 和错误位置（JSON 路径、行）：" + message,
            invalid.StatusCode == HttpStatusCode.BadRequest && message.Contains("elements[0]", StringComparison.Ordinal) && message.Contains("第 1 行", StringComparison.Ordinal));

        var duplicated = template.DeepClone().AsObject();
        duplicated["elements"]!.AsArray()[1]!["id"] = duplicated["elements"]!.AsArray()[0]!["id"]!.GetValue<string>();
        var refused = await admin.PostAsync("/api/admin/preview", new
        {
            template = duplicated
        });
        check("模板有结构问题（元素 ID 重复）时 400 并逐条列出",
            await AdminSession.CodeAsync(refused) == "INVALID" && (await AdminSession.ReadAsync(refused)).GetProperty("details").EnumerateArray().Any(item => item.GetString()!.Contains("重复")));

        using var keyed = new HttpRequestMessage(HttpMethod.Post, "/api/admin/preview") { Content = JsonContent.Create(new { template }) };
        keyed.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        check("API Key 不能调预览（预览在管理接口，只认登录）", (await http.SendAsync(keyed)).StatusCode == HttpStatusCode.Unauthorized);

        // ADR 0007 采纳后开放接口不再有预览的占位（原来返回 501），带 Key 或登录令牌都是 404。
        using var publicKeyed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview") { Content = JsonContent.Create(new { template }) };
        publicKeyed.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        check("开放接口没有 /api/v1/preview（ADR 0007），带 API Key 或登录令牌都是 404",
            (await http.SendAsync(publicKeyed)).StatusCode == HttpStatusCode.NotFound
            && (await admin.PostAsync("/api/v1/preview", new
            {
                template
            })).StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// 字体下载：fonts.css 按模板字体名声明 @font-face，字体文件和渲染用的是同一份、带缓存版本号。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckFontFilesAsync(HttpClient http, Action<string, bool> check)
    {
        var css = await http.GetStringAsync("/fonts/fonts.css");
        var url = System.Text.RegularExpressions.Regex.Match(css, "url\\(\"(?<url>[^\"]+)\"\\)").Groups["url"].Value;
        check("fonts.css 不用登录，按模板字体名声明 @font-face，可变字体带字重范围",
            css.Contains("font-family: \"LabelService SourceHanSansSC\"", StringComparison.Ordinal)
            && css.Contains("font-family: \"LabelService SourceHanSerifSC\"", StringComparison.Ordinal)
            && css.Contains("font-weight: 100 900", StringComparison.Ordinal) && url.Contains(".ttf?v=", StringComparison.Ordinal));

        var font = await http.GetAsync("/fonts/" + url);
        var bytes = await font.Content.ReadAsByteArrayAsync();
        check("字体文件：font/ttf、长缓存，是渲染用的同一个文件",
            font.StatusCode == HttpStatusCode.OK && font.Content.Headers.ContentType?.MediaType == "font/ttf"
            && font.Headers.CacheControl?.MaxAge >= TimeSpan.FromDays(365) && bytes.Length > 1_000_000);
        check("只提供已加载的字体文件，别的文件 404",
            (await http.GetAsync("/fonts/appsettings.json")).StatusCode == HttpStatusCode.NotFound
            && (await http.GetAsync("/fonts/..%2Fappsettings.json")).StatusCode == HttpStatusCode.NotFound
            && (await http.GetAsync("/fonts/missing.ttf")).StatusCode == HttpStatusCode.NotFound);
    }
}
