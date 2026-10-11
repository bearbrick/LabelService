using System.Text;
using System.Text.Json;
using LabelService.Contracts.Protocol;

namespace LabelService.Backend.Checks;

/// <summary>
/// 字体由客户上传（ADR 0011）：部署目录没有字体的新服务，上传思源黑体后立即生效（不用重启），删除后降级；权限、校验、操作日志。
/// </summary>
internal static class FontUploadChecks
{
    /// <summary>
    /// 上传用的字体文件名（带空格，检查文件名原样保留和删除时的地址编码）。
    /// </summary>
    private const string FontFileName = "Noto Sans SC.ttf";

    /// <summary>
    /// 用空的部署字体目录启动一个服务，跑字体上传、删除的检查，结束后删除临时目录。
    /// </summary>
    /// <param name="backendRoot">backend 目录（从 assets/fonts 读思源黑体作为上传的文件）。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        var dataDirectory = TempDataDirectory.Create();
        var emptyFonts = Path.Combine(dataDirectory, "deploy-fonts");
        Directory.CreateDirectory(emptyFonts);
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory, new Dictionary<string, string?>
            {
                ["LabelService:Fonts:Directory"] = emptyFonts
            });
            using var http = factory.CreateClient();
            var admin = await LoginAdminAsync(http);
            var (viewer, _) = await AdminAccountChecks.CreateUserAsync(http, admin, "font-viewer", "只读访客", true, []);

            var initial = await admin.JsonAsync(HttpMethod.Get, "/api/admin/fonts");
            check("部署目录没有字体时：字体列表为空、没有默认字体，生成标签时文本降级（FONT_FALLBACK）",
                initial.GetProperty("items").GetArrayLength() == 0
                && !initial.GetProperty("hasDefaultFamily").GetBoolean()
                && (await RenderWarningsAsync(http, backendRoot)).Contains(WarningCodes.FontFallback, StringComparison.Ordinal));

            var font = await File.ReadAllBytesAsync(Path.Combine(backendRoot, "assets", "fonts", "NotoSansSC-VF.ttf"));
            check("没有 res.manage 不能上传字体",
                await AdminSession.CodeAsync(await viewer.UploadAsync("/api/admin/fonts", FontFileName, font, "font/ttf")) == "FORBIDDEN");

            var uploaded = await AdminSession.ReadAsync(await admin.UploadAsync("/api/admin/fonts", FontFileName, font, "font/ttf"));
            var item = uploaded.GetProperty("items").EnumerateArray().FirstOrDefault(entry => entry.GetProperty("fileName").GetString() == FontFileName);
            check("上传思源黑体后不用重启就生效：列表里是上传的文件，模板字体名有 SourceHanSansSC，生成标签不再降级",
                item.ValueKind == JsonValueKind.Object
                && item.GetProperty("uploaded").GetBoolean()
                && item.GetProperty("size").GetInt64() == font.Length
                && item.GetProperty("families").EnumerateArray().Any(name => name.GetString() == "Noto Sans SC")
                && uploaded.GetProperty("templateFamilies").EnumerateArray().Any(name => name.GetString() == "SourceHanSansSC")
                && uploaded.GetProperty("hasDefaultFamily").GetBoolean()
                && !(await RenderWarningsAsync(http, backendRoot)).Contains(WarningCodes.FontFallback, StringComparison.Ordinal));

            var css = await http.GetStringAsync("/fonts/fonts.css");
            var file = await http.GetAsync("/fonts/" + Uri.EscapeDataString(FontFileName));
            check("fonts.css 声明了上传的字体，设计器能下载同一份字体文件",
                css.Contains("LabelService SourceHanSansSC", StringComparison.Ordinal) && file.IsSuccessStatusCode
                && (await file.Content.ReadAsByteArrayAsync()).Length == font.Length);

            check("同名文件、不是字体的文件、不支持的扩展名都拒绝",
                await AdminSession.CodeAsync(await admin.UploadAsync("/api/admin/fonts", FontFileName, font, "font/ttf")) == "INVALID"
                && await AdminSession.CodeAsync(await admin.UploadAsync("/api/admin/fonts", "fake.ttf", Encoding.UTF8.GetBytes("hello"), "font/ttf")) == "INVALID"
                && await AdminSession.CodeAsync(await admin.UploadAsync("/api/admin/fonts", "font.zip", font, "application/zip")) == "INVALID");

            check("没有 res.manage 不能删除字体；不存在的字体返回 404",
                await AdminSession.CodeAsync(await viewer.DeleteAsync("/api/admin/fonts/" + Uri.EscapeDataString(FontFileName))) == "FORBIDDEN"
                && await AdminSession.CodeAsync(await admin.DeleteAsync("/api/admin/fonts/nope.ttf")) == "NOT_FOUND");

            var deleted = await admin.JsonAsync(HttpMethod.Delete, "/api/admin/fonts/" + Uri.EscapeDataString(FontFileName));
            check("删除上传的字体后立即生效：文件删掉了（没有被占用），生成标签又降级",
                deleted.GetProperty("items").GetArrayLength() == 0
                && !File.Exists(Path.Combine(dataDirectory, "fonts", FontFileName))
                && (await RenderWarningsAsync(http, backendRoot)).Contains(WarningCodes.FontFallback, StringComparison.Ordinal));

            var ops = await admin.JsonAsync(HttpMethod.Get, "/api/admin/op-logs?type=" + Uri.EscapeDataString("字体"));
            var details = ops.EnumerateArray().Select(entry => entry.GetProperty("detail").GetString() ?? "").ToList();
            check("上传、删除字体都记了操作日志",
                details.Any(detail => detail.StartsWith("上传", StringComparison.Ordinal)) && details.Any(detail => detail.StartsWith("删除", StringComparison.Ordinal)));
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    /// <summary>
    /// 新服务的 admin 登录并改掉初始密码。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <returns>已登录、改过密码的 admin 会话。</returns>
    private static async Task<AdminSession> LoginAdminAsync(HttpClient http)
    {
        var (_, first, _) = await AdminSession.LoginAsync(http, "admin", "admin123");
        await first!.PostAsync("/api/admin/me/password", new
        {
            oldPassword = "admin123",
            newPassword = AdminAccountChecks.AdminPassword
        });
        var (_, admin, _) = await AdminSession.LoginAsync(http, "admin", AdminAccountChecks.AdminPassword);
        return admin!;
    }

    /// <summary>
    /// 用开发 Key 生成一张示例箱标（模板里的文字用思源黑体），返回响应头里的警告。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="backendRoot">backend 目录（读示例请求）。</param>
    /// <returns>X-Label-Warnings 的内容；没有警告时为空字符串。</returns>
    private static async Task<string> RenderWarningsAsync(HttpClient http, string backendRoot)
    {
        var body = await File.ReadAllTextAsync(Path.Combine(backendRoot, "samples", "requests", "box-label.render.json"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        using var response = await http.SendAsync(request);
        return response.Headers.TryGetValues(HeaderNames.LabelWarnings, out var values) ? string.Join(",", values) : "";
    }
}
