using System.Net;
using System.Text;
using System.Text.Json;
using LabelService.Contracts.Protocol;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// 系统信息和公司 Logo：未登录可读、没有 sys.settings 不能改、长度校验、上传和恢复 Logo、操作日志。
/// </summary>
internal static class SystemInfoChecks
{
    /// <summary>
    /// 运行系统信息的检查。
    /// </summary>
    /// <param name="http">未登录的客户端。</param>
    /// <param name="admin">已登录的系统管理员。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(HttpClient http, AdminSession admin, Action<string, bool> check)
    {
        var anonymous = new AdminSession(http, null);
        var initial = await anonymous.JsonAsync(HttpMethod.Get, "/api/admin/system-info");
        check("未登录也能读系统信息；没保存过时是默认值，没有 Logo",
            initial.GetProperty("systemName").GetString() == "标签服务"
            && initial.GetProperty("subtitle").GetString() == "标签设计与生成工作台"
            && initial.GetProperty("companyName").GetString() == ""
            && NoLogo(initial));
        check("没上传 Logo 时取 Logo 返回 404", (await http.GetAsync("/api/admin/system-info/logo")).StatusCode == HttpStatusCode.NotFound);

        var (designer, _) = await AdminAccountChecks.CreateUserAsync(http, admin, "sysinfo-designer", "模板设计员", true, []);
        var body = new
        {
            systemName = "  标签平台\n",
            subtitle = "包装标签",
            companyName = "示例制造有限公司",
            copyright = "© 2026 示例制造有限公司 版权所有",
            support = "信息部 分机 8001",
        };
        check("没有 sys.settings 不能改系统信息、不能上传 Logo",
            await AdminSession.CodeAsync(await designer.PutAsync("/api/admin/system-info", body)) == "FORBIDDEN"
            && await AdminSession.CodeAsync(await designer.UploadAsync("/api/admin/system-info/logo", "logo.png", Png(), "image/png")) == "FORBIDDEN"
            && await AdminSession.CodeAsync(await anonymous.PutAsync("/api/admin/system-info", body)) == "UNAUTHORIZED");

        var saved = await admin.JsonAsync(HttpMethod.Put, "/api/admin/system-info", body);
        var read = await anonymous.JsonAsync(HttpMethod.Get, "/api/admin/system-info");
        check("系统管理员修改后立即生效：去掉首尾空白、换行换成空格",
            saved.GetProperty("systemName").GetString() == "标签平台"
            && read.GetProperty("companyName").GetString() == "示例制造有限公司"
            && read.GetProperty("copyright").GetString() == "© 2026 示例制造有限公司 版权所有"
            && read.GetProperty("support").GetString() == "信息部 分机 8001");
        check("系统名称不能为空、超长拒绝，整体不保存",
            await AdminSession.CodeAsync(await admin.PutAsync("/api/admin/system-info", new
            {
                systemName = " ",
                subtitle = "x"
            })) == "INVALID"
            && await AdminSession.CodeAsync(await admin.PutAsync("/api/admin/system-info", new
            {
                systemName = "标签平台",
                copyright = new string('版', 121)
            })) == "INVALID"
            && (await anonymous.JsonAsync(HttpMethod.Get, "/api/admin/system-info")).GetProperty("subtitle").GetString() == "包装标签");

        check("登录页标语和说明默认有值，标语可以换行",
            read.GetProperty("loginTitle").GetString()!.Contains('\n', StringComparison.Ordinal)
            && read.GetProperty("loginIntro").GetString()!.Length > 0);
        var login = await admin.JsonAsync(HttpMethod.Put, "/api/admin/system-info", new
        {
            loginTitle = "第一行\r\n第二行",
            loginIntro = "  说明  ",
        });
        check("只改登录页标语和说明：换行保留（统一成 \n），没传的项不变",
            login.GetProperty("loginTitle").GetString() == "第一行\n第二行"
            && login.GetProperty("loginIntro").GetString() == "说明"
            && login.GetProperty("companyName").GetString() == "示例制造有限公司"
            && login.GetProperty("systemName").GetString() == "标签平台");

        var uploaded = await AdminSession.ReadAsync(await admin.UploadAsync("/api/admin/system-info/logo", "logo.png", Png(), "image/png"));
        var version = uploaded.GetProperty("logoVersion").GetString();
        var logo = await http.GetAsync("/api/admin/system-info/logo?v=" + version);
        check("上传 PNG Logo 后未登录也能取到，带 nosniff 和内容安全策略",
            version is { Length: 12 }
            && logo.StatusCode == HttpStatusCode.OK
            && logo.Content.Headers.ContentType?.MediaType == "image/png"
            && logo.Headers.TryGetValues("X-Content-Type-Options", out var sniff) && sniff.Contains("nosniff")
            && logo.Headers.Contains("Content-Security-Policy"));

        var svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10" onload="alert(1)"><script>alert(2)</script><rect width="10" height="10"/></svg>""";
        var svgInfo = await AdminSession.ReadAsync(await admin.UploadAsync("/api/admin/system-info/logo", "logo.svg", Encoding.UTF8.GetBytes(svg), "image/svg+xml"));
        var svgText = await (await http.GetAsync("/api/admin/system-info/logo")).Content.ReadAsStringAsync();
        check("换成 SVG Logo：版本变化，脚本和事件属性被清洗掉",
            svgInfo.GetProperty("logoVersion").GetString() != version
            && svgText.Contains("<rect", StringComparison.Ordinal) && !svgText.Contains("script", StringComparison.Ordinal) && !svgText.Contains("onload", StringComparison.Ordinal));
        check("Logo 不是图片、超过 1 MB 都拒绝",
            await AdminSession.CodeAsync(await admin.UploadAsync("/api/admin/system-info/logo", "a.txt", Encoding.UTF8.GetBytes("hello"), "text/plain")) == "INVALID"
            && await AdminSession.CodeAsync(await admin.UploadAsync("/api/admin/system-info/logo", "big.png", new byte[RenderLimits.MaxImageBytes + 1], "image/png")) == "INVALID");

        var reset = await admin.JsonAsync(HttpMethod.Delete, "/api/admin/system-info/logo");
        check("恢复默认 Logo 后版本为空，取 Logo 返回 404",
            NoLogo(reset)
            && (await http.GetAsync("/api/admin/system-info/logo")).StatusCode == HttpStatusCode.NotFound);

        var ops = await admin.JsonAsync(HttpMethod.Get, "/api/admin/op-logs?type=" + Uri.EscapeDataString("系统信息"));
        var details = ops.EnumerateArray().Select(item => item.GetProperty("detail").GetString() ?? "").ToList();
        check("修改系统信息、上传和恢复 Logo 都记了操作日志",
            details.Any(item => item.Contains("系统名称", StringComparison.Ordinal) && item.Contains("所属公司", StringComparison.Ordinal))
            && details.Count(item => item.StartsWith("上传", StringComparison.Ordinal)) == 2
            && details.Any(item => item.StartsWith("恢复", StringComparison.Ordinal)));

        var permissions = await admin.JsonAsync(HttpMethod.Get, "/api/admin/permissions");
        check("权限点列表里有 sys.settings（系统分组）",
            permissions.EnumerateArray().Any(item => item.GetProperty("key").GetString() == "sys.settings" && item.GetProperty("group").GetString() == "系统"));

        await CheckIconAndWebpAsync(http, admin, designer, check);
    }

    /// <summary>
    /// Logo 图标（顶栏、浏览器标签页用）的上传、读取、恢复；Logo 和图标接受 WebP，素材仍然不收。
    /// </summary>
    /// <param name="http">未登录的客户端。</param>
    /// <param name="admin">已登录的系统管理员。</param>
    /// <param name="designer">没有 sys.settings 的用户。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckIconAndWebpAsync(HttpClient http, AdminSession admin, AdminSession designer, Action<string, bool> check)
    {
        check("没上传图标时取图标返回 404；没有 sys.settings 不能上传图标",
            (await http.GetAsync("/api/admin/system-info/icon")).StatusCode == HttpStatusCode.NotFound
            && await AdminSession.CodeAsync(await designer.UploadAsync("/api/admin/system-info/icon", "icon.png", Png(), "image/png")) == "FORBIDDEN");

        var icon = await AdminSession.ReadAsync(await admin.UploadAsync("/api/admin/system-info/icon", "icon.png", Png(), "image/png"));
        var iconFile = await http.GetAsync("/api/admin/system-info/icon?v=" + icon.GetProperty("iconVersion").GetString());
        check("上传图标后未登录也能取到，和完整 Logo 互不影响",
            icon.GetProperty("iconVersion").GetString() is { Length: 12 }
            && NoLogo(icon)
            && iconFile.StatusCode == HttpStatusCode.OK && iconFile.Content.Headers.ContentType?.MediaType == "image/png");

        var webp = await AdminSession.ReadAsync(await admin.UploadAsync("/api/admin/system-info/logo", "logo.webp", Webp(), "image/webp"));
        var webpFile = await http.GetAsync("/api/admin/system-info/logo");
        check("完整 Logo 接受 WebP（按内容判断，返回 image/webp）",
            !NoLogo(webp) && webpFile.Content.Headers.ContentType?.MediaType == "image/webp");
        check("素材仍然不收 WebP（素材要进渲染）",
            await AdminSession.CodeAsync(await admin.UploadAsync("/api/admin/assets", "a.webp", Webp(), "image/webp")) == "INVALID");

        var reset = await admin.JsonAsync(HttpMethod.Delete, "/api/admin/system-info/icon");
        check("恢复图标后图标版本为空、取图标 404，完整 Logo 还在",
            (!reset.TryGetProperty("iconVersion", out var version) || version.ValueKind == JsonValueKind.Null)
            && !NoLogo(reset)
            && (await http.GetAsync("/api/admin/system-info/icon")).StatusCode == HttpStatusCode.NotFound);
        await admin.DeleteAsync("/api/admin/system-info/logo");
    }

    /// <summary>
    /// 上传用的 32×16 蓝色 WebP。
    /// </summary>
    /// <returns>WebP 文件内容。</returns>
    private static byte[] Webp()
    {
        using var bitmap = new SKBitmap(32, 16);
        bitmap.Erase(SKColors.RoyalBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Webp, 90);
        return data.ToArray();
    }

    /// <summary>
    /// 系统信息里没有 Logo 版本（为 null 时接口不输出这个属性）。
    /// </summary>
    /// <param name="info">系统信息。</param>
    /// <returns>没有 Logo 时为 true。</returns>
    private static bool NoLogo(JsonElement info) =>
        !info.TryGetProperty("logoVersion", out var version) || version.ValueKind == JsonValueKind.Null;

    /// <summary>
    /// 上传 Logo 用的 32×16 蓝色 PNG。
    /// </summary>
    /// <returns>PNG 文件内容。</returns>
    private static byte[] Png()
    {
        using var bitmap = new SKBitmap(32, 16);
        bitmap.Erase(SKColors.RoyalBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
