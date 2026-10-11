using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Domain.Logs;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// 拼版的接口：开放接口的 layout、startSlot、X-Page-Count、错误码、张数上限、调用日志；
/// 管理接口的版式增删改、检查、启用停用、预览和打印校准测试页。
/// </summary>
internal static partial class LayoutServerChecks
{
    /// <summary>
    /// 示例版式的编码。
    /// </summary>
    private const string Layout = "A4_100x60_2x4";

    /// <summary>
    /// 开放接口的拼版检查：用临时数据目录启动服务，单张、批量、错误码和调用日志。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory);
            using var http = factory.CreateClient();
            var data = File.ReadAllText(Path.Combine(backendRoot, "samples", "requests", "box-label.render.json"));
            var sample = JsonDocument.Parse(data).RootElement.GetProperty("data").GetRawText();
            await CheckSheetsAsync(http, sample, check);
            await CheckErrorsAsync(http, sample, check);

            var logs = factory.Services.GetRequiredService<IRenderLogRepository>();
            IReadOnlyList<LabelService.Domain.Logs.RenderLogEntity> records = [];
            for (var i = 0; i < 100 && !records.Any(log => log.LayoutCode == Layout); i++)
            {
                await Task.Delay(50);
                records = await logs.SearchAsync(new RenderLogFilter(PageSize: 200));
            }

            check("调用日志记下版式编码（render_log.layout_code），不拼版的为空",
                records.Any(log => log.LayoutCode == Layout && log.Status == "ok") && records.Any(log => log.LayoutCode is null && log.Status == "ok"));
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    /// <summary>
    /// 管理接口的版式检查，用 <see cref="AdminChecks"/> 登录好的管理员。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="admin">已登录并改过密码的 admin 会话。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAdminAsync(HttpClient http, AdminSession admin, Action<string, bool> check)
    {
        var list = await admin.JsonAsync(HttpMethod.Get, "/api/admin/layouts");
        var matching = await admin.JsonAsync(HttpMethod.Get, "/api/admin/layouts?slotWidth=100&slotHeight=60");
        var other = await admin.JsonAsync(HttpMethod.Get, "/api/admin/layouts?slotWidth=210&slotHeight=297");
        check("版式列表：开发环境导入了示例版式；按格子尺寸筛选只列出能用的",
            list.EnumerateArray().Any(item => item.GetProperty("layout").GetProperty("code").GetString() == Layout)
            && matching.GetArrayLength() >= 1 && other.GetArrayLength() == 0);

        var bad = await admin.PostAsync("/api/admin/layouts", new
        {
            layout = NewLayout("A4_BAD", 90),
        });
        var problems = await AdminSession.ReadAsync(bad);
        check("保存前检查版式：格子重叠返回 INVALID，明细写清是哪两个格子",
            problems.GetProperty("code").GetString() == "INVALID" && problems.ToString().Contains("第 2 个格子和第 1 个格子重叠"));

        var created = await admin.PostAsync("/api/admin/layouts", new
        {
            layout = NewLayout("A4_TWO", 105.04),
        });
        var view = await AdminSession.ReadAsync(created);
        check("新建版式：长度按 0.1 mm 取整（105.04 → 105），默认启用",
            created.IsSuccessStatusCode && view.GetProperty("enabled").GetBoolean()
            && view.GetProperty("layout").GetProperty("slots")[1].GetProperty("x").GetDouble() == 105);
        check("版式编码重复时拒绝", await AdminSession.CodeAsync(await admin.PostAsync("/api/admin/layouts", new
        {
            layout = NewLayout("A4_TWO", 105)
        })) == "INVALID");

        var used = await ServerChecks.Post(http, """{"templateCode":"BOX_LABEL","format":"pdf","copies":3,"layout":"A4_TWO","data":{"partName":"电源适配器","partNo":"3100-0123","qty":50,"batchNo":"20261008-A","boxNo":"C2610080001"}}""");
        check("新建的版式立即能用：2 格的版式 3 份是 2 页", used.IsSuccessStatusCode && ServerChecks.Header(used, HeaderNames.PageCount) == "2");

        var disabled = await admin.PutAsync("/api/admin/layouts/A4_TWO", new
        {
            layout = NewLayout("A4_TWO", 105),
            enabled = false
        });
        var afterDisable = await ServerChecks.Post(http, """{"templateCode":"BOX_LABEL","format":"pdf","layout":"A4_TWO","data":{"partName":"a","partNo":"1","qty":1,"batchNo":"b","boxNo":"C1"}}""");
        check("停用后开放接口立即返回 LAYOUT_NOT_FOUND；改编码被拒绝",
            disabled.IsSuccessStatusCode && (int)afterDisable.StatusCode == 404 && (await ReadCode(afterDisable)) == ErrorCodes.LayoutNotFound
            && await AdminSession.CodeAsync(await admin.PutAsync("/api/admin/layouts/A4_TWO", new
            {
                layout = NewLayout("A4_OTHER", 105)
            })) == "INVALID");

        var (viewer, _) = await AdminAccountChecks.CreateUserAsync(http, admin, "layoutviewer", "只读访客", true, []);
        check("没有 res.manage 的用户能看版式，不能新建", (await viewer.GetAsync("/api/admin/layouts")).IsSuccessStatusCode
            && await AdminSession.CodeAsync(await viewer.PostAsync("/api/admin/layouts", new
            {
                layout = NewLayout("A4_VIEW", 105)
            })) == "FORBIDDEN");

        var preview = await admin.PostAsync("/api/admin/layouts/preview", new
        {
            layout = NewLayout("UNSAVED", 105),
            templateCode = "BOX_LABEL"
        });
        using (var bitmap = SKBitmap.Decode(await preview.Content.ReadAsByteArrayAsync()))
        {
            check("预览还没保存的版式：不给数据时用样例值铺满一页，返回 PNG（A4 300 dpi）",
                preview.Content.Headers.ContentType?.MediaType == "image/png" && bitmap is { Width: 2480, Height: 3508 }
                && ServerChecks.Header(preview, HeaderNames.LabelCount) == "2" && ServerChecks.Header(preview, HeaderNames.PageCount) == "1");
        }

        var testPage = await admin.PostAsync("/api/admin/layouts/test-page", new
        {
            layout = NewLayout("UNSAVED", 105)
        });
        var pdf = Encoding.Latin1.GetString(await testPage.Content.ReadAsByteArrayAsync());
        check("打印校准测试页：默认 PDF，一页，每个格子一张（边框、中心十字、序号）",
            testPage.Content.Headers.ContentType?.MediaType == "application/pdf" && PdfPage().Count(pdf) == 1 && ServerChecks.Header(testPage, HeaderNames.LabelCount) == "2");

        check("删除版式后详情返回 404", (await admin.DeleteAsync("/api/admin/layouts/A4_TWO")).IsSuccessStatusCode
            && (int)(await admin.GetAsync("/api/admin/layouts/A4_TWO")).StatusCode == 404);
    }

    /// <summary>
    /// 拼版输出：单张多份、批量加起始格子的页数和张数，PNG 单张多份、批量 PNG 打包 ZIP，不传 layout 时和原来一样。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="data">示例箱标的数据（JSON 对象文本）。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckSheetsAsync(HttpClient http, string data, Action<string, bool> check)
    {
        var single = await ServerChecks.Post(http, Single("pdf", data, copies: 8));
        check("单张 copies 8 + layout A4_100x60_2x4：PDF 1 页 8 张，X-Page-Count 1、X-Label-Count 8",
            single.IsSuccessStatusCode && ServerChecks.Header(single, HeaderNames.PageCount) == "1" && ServerChecks.Header(single, HeaderNames.LabelCount) == "8"
            && PdfPage().Count(Encoding.Latin1.GetString(await single.Content.ReadAsByteArrayAsync())) == 1);

        var batch = await PostBatch(http, Batch("pdf", data, 19, ""","startSlot":3"""));
        check("批量 19 张 + startSlot 3：3 页（6 + 8 + 5），X-Page-Count 3、X-Label-Count 19",
            batch.IsSuccessStatusCode && ServerChecks.Header(batch, HeaderNames.PageCount) == "3" && ServerChecks.Header(batch, HeaderNames.LabelCount) == "19"
            && PdfPage().Count(Encoding.Latin1.GetString(await batch.Content.ReadAsByteArrayAsync())) == 3);

        var png = await ServerChecks.Post(http, Single("png", data, copies: 8));
        check("用了版式时 PNG 单张也能多份：一页 8 张，一张 PNG", png.IsSuccessStatusCode && png.Content.Headers.ContentType?.MediaType == "image/png");

        var pngBatch = await PostBatch(http, Batch("png", data, 10, ""));
        using var zip = new ZipArchive(new MemoryStream(await pngBatch.Content.ReadAsByteArrayAsync()));
        check("批量 PNG 用了版式时 output 不起作用：两页打包 ZIP，文件名 模板编码_页号.png",
            pngBatch.Content.Headers.ContentType?.MediaType == "application/zip" && ServerChecks.Header(pngBatch, HeaderNames.PageCount) == "2"
            && zip.Entries.Select(entry => entry.FullName).SequenceEqual(["BOX_LABEL_001.png", "BOX_LABEL_002.png"]));

        var plain = await ServerChecks.Post(http, Single("pdf", data, layout: null));
        check("不传 layout 时和原来一样：没有 X-Page-Count 头", plain.IsSuccessStatusCode && ServerChecks.Header(plain, HeaderNames.PageCount) is null);
    }

    /// <summary>
    /// 拼版的错误码：ZPL 不能拼版、版式不存在、起始格子越界、格子尺寸和模板不符、张数超过上限。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="data">示例箱标的数据（JSON 对象文本）。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckErrorsAsync(HttpClient http, string data, Action<string, bool> check)
    {
        check("ZPL 加版式返回 400 FORMAT_INVALID", await Code(await ServerChecks.Post(http, Single("zpl", data))) == (400, ErrorCodes.FormatInvalid));
        check("版式不存在返回 404 LAYOUT_NOT_FOUND", await Code(await ServerChecks.Post(http, Single("pdf", data, layout: "A4_NOPE"))) == (404, ErrorCodes.LayoutNotFound));
        check("模板尺寸和格子尺寸不一致返回 400 LAYOUT_SIZE_MISMATCH",
            await Code(await ServerChecks.Post(http, "{\"templateCode\":\"A4_SHIPPING_MARK\",\"format\":\"pdf\",\"layout\":\"" + Layout + "\",\"data\":{}}")) == (400, ErrorCodes.LayoutSizeMismatch));
        check("startSlot 是 0 或超过格子数返回 400 START_SLOT_INVALID",
            await Code(await ServerChecks.Post(http, Single("pdf", data, startSlot: 0))) == (400, ErrorCodes.StartSlotInvalid)
            && await Code(await ServerChecks.Post(http, Single("pdf", data, startSlot: 9))) == (400, ErrorCodes.StartSlotInvalid));
        check("没用版式时 PNG 单张多份仍然返回 COPIES_INVALID",
            await Code(await ServerChecks.Post(http, Single("png", data, copies: 2, layout: null))) == (400, ErrorCodes.CopiesInvalid));
        var tooMany = await PostBatch(http, Batch("pdf", data, 6, "", copies: 100));
        check("拼版的张数上限按标签张数算：6 条 × 100 份返回 413 BATCH_TOO_LARGE", await Code(tooMany) == (413, ErrorCodes.BatchTooLarge));
    }

    /// <summary>
    /// 新建版式的请求体：A4 上两个 100×60 的格子。
    /// </summary>
    /// <param name="code">版式编码。</param>
    /// <param name="secondX">第二个格子的横坐标，mm（改小可以造出重叠的格子）。</param>
    /// <returns>版式 JSON（匿名对象）。</returns>
    private static object NewLayout(string code, double secondX) => new
    {
        code,
        name = "两格 " + code,
        paper = new
        {
            width = 210,
            height = 297
        },
        slot = new
        {
            width = 100,
            height = 60
        },
        slots = new[] { new { x = 5.0, y = 10.0, rotation = 0 }, new { x = secondX, y = 10.0, rotation = 0 } },
        offset = new
        {
            x = 0,
            y = 0
        },
        cutLines = true,
    };

    /// <summary>
    /// 单张生成的请求体。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <param name="data">字段数据（JSON 对象文本）。</param>
    /// <param name="copies">份数。</param>
    /// <param name="layout">版式编码；null 表示不拼版。</param>
    /// <param name="startSlot">起始格子；null 表示不传。</param>
    /// <returns>请求体 JSON。</returns>
    private static string Single(string format, string data, int copies = 1, string? layout = Layout, int? startSlot = null)
    {
        var extra = (layout is null ? "" : $$""","layout":"{{layout}}" """.TrimEnd()) + (startSlot is null ? "" : $$""","startSlot":{{startSlot}}""");
        return $$"""{"templateCode":"BOX_LABEL","format":"{{format}}","copies":{{copies}}{{extra}},"data":""" + data + "}";
    }

    /// <summary>
    /// 批量生成的请求体（拼版）：公共字段用示例数据，每条只改箱号。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <param name="data">公共字段（JSON 对象文本）。</param>
    /// <param name="count">条数。</param>
    /// <param name="extra">额外的属性，如 ,"startSlot":3。</param>
    /// <param name="copies">每条的份数。</param>
    /// <returns>请求体 JSON。</returns>
    private static string Batch(string format, string data, int count, string extra, int copies = 1)
    {
        var items = string.Join(",", Enumerable.Range(1, count).Select(i => $$"""{"data":{"boxNo":"C26100800{{i:D2}}"},"copies":{{copies}}""" + "}"));
        return $$"""{"templateCode":"BOX_LABEL","format":"{{format}}","layout":"{{Layout}}"{{extra}},"common":{{data}},"items":[{{items}}]}""";
    }

    /// <summary>
    /// 用开发 Key 调批量接口。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="json">请求体。</param>
    /// <returns>响应。</returns>
    private static async Task<HttpResponseMessage> PostBatch(HttpClient http, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render/batch") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        return await http.SendAsync(request);
    }

    /// <summary>
    /// 响应的状态码和错误码。
    /// </summary>
    /// <param name="response">响应。</param>
    /// <returns>状态码和错误码（成功时错误码为 null）。</returns>
    private static async Task<(int Status, string? Code)> Code(HttpResponseMessage response) => ((int)response.StatusCode, await ReadCode(response));

    /// <summary>
    /// 读开放接口错误响应里的错误码。
    /// </summary>
    /// <param name="response">响应。</param>
    /// <returns>错误码；成功时为 null。</returns>
    private static async Task<string?> ReadCode(HttpResponseMessage response) =>
        response.IsSuccessStatusCode ? null : (await response.Content.ReadFromJsonAsync<ErrorResponse>(LabelJson.Options))?.Code;

    /// <summary>
    /// PDF 里的页对象（/Type /Page，不含 /Pages）。
    /// </summary>
    /// <returns>编译好的正则。</returns>
    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PdfPage();
}
