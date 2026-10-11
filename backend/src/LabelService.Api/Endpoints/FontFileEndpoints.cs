using System.Globalization;
using System.Text;
using LabelService.Rendering.Fonts;

namespace LabelService.Api.Endpoints;

/// <summary>
/// 把服务端渲染用的字体文件提供给设计器（设计书第 7 节“字体”）：浏览器和服务端用同一份字体，排版才能一致。
/// </summary>
/// <remarks>
/// <para>
/// <c>GET /fonts/fonts.css</c>：每个模板字体名一条 @font-face，家族名是“LabelService + 模板字体名”（如 LabelService SourceHanSansSC），
/// 指向渲染时实际用的那个文件；可变字体按字重范围 100～900 声明。设计器用 &lt;link&gt; 引入。
/// 同时用 ascent-override、descent-override 把浏览器的字体度量设成服务端排版用的值：浏览器（Konva）和服务端算基线的公式一样
/// （行内按 ascent、descent 居中），但度量来源不同（浏览器和 Skia 取的字体表不一样）会差零点几毫米。
/// </para>
/// <para>
/// <c>GET /fonts/{文件名}</c>：字体文件，只认已加载的文件名。地址带文件修改时间做版本号，所以可以长缓存。
/// </para>
/// <para>
/// 不需要登录：@font-face 不能带 Authorization 头，字体本身是 SIL OFL 授权，可以公开分发。
/// </para>
/// </remarks>
internal static class FontFileEndpoints
{
    /// <summary>
    /// 设计器里字体家族名的前缀，避免和本机安装的同名字体混淆。
    /// </summary>
    public const string CssFamilyPrefix = "LabelService ";

    private static readonly Dictionary<string, (string Format, string ContentType)> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".ttf"] = ("truetype", "font/ttf"),
        [".otf"] = ("opentype", "font/otf"),
        [".ttc"] = ("collection", "font/collection"),
        [".otc"] = ("collection", "font/collection"),
    };

    /// <summary>
    /// 注册路由。
    /// </summary>
    public static IEndpointRouteBuilder MapFontFiles(this IEndpointRouteBuilder app)
    {
        app.MapGet("/fonts/fonts.css", (HttpContext http, FontRegistry fonts) =>
        {
            http.Response.Headers.CacheControl = "no-cache";
            return Results.Text(Css(fonts), "text/css; charset=utf-8");
        });
        app.MapGet("/fonts/{file}", (string file, HttpContext http, FontRegistry fonts) =>
        {
            var path = fonts.Files.FirstOrDefault(item => string.Equals(Path.GetFileName(item), file, StringComparison.OrdinalIgnoreCase));
            if (path is null || !Types.TryGetValue(Path.GetExtension(path), out var type))
            {
                return Results.NotFound();
            }

            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(path, type.ContentType, lastModified: File.GetLastWriteTimeUtc(path), enableRangeProcessing: true);
        });
        return app;
    }

    /// <summary>
    /// 生成 fonts.css。TrueType / OpenType 集合（.ttc、.otc）浏览器不支持，跳过，设计器会退回本机字体。
    /// </summary>
    internal static string Css(FontRegistry fonts)
    {
        var css = new StringBuilder("/* 标签服务的字体：和服务端渲染用同一份文件。 */\n");
        foreach (var family in FontRegistry.TemplateFamilies)
        {
            if (fonts.SourceOf(family) is not { } source
                || !Types.TryGetValue(Path.GetExtension(source.Path), out var type) || type.Format == "collection")
            {
                continue;
            }

            var version = File.GetLastWriteTimeUtc(source.Path).Ticks.ToString(CultureInfo.InvariantCulture);
            var url = Uri.EscapeDataString(Path.GetFileName(source.Path)) + "?v=" + version;
            css.Append("@font-face {\n")
                .Append(CultureInfo.InvariantCulture, $"  font-family: \"{CssFamilyPrefix}{family}\";\n")
                .Append(CultureInfo.InvariantCulture, $"  src: url(\"{url}\") format(\"{type.Format}\");\n")
                .Append(source.IsVariable ? "  font-weight: 100 900;\n" : "  font-weight: 400;\n")
                .Append(CultureInfo.InvariantCulture, $"  ascent-override: {source.Ascent * 100:0.###}%;\n")
                .Append(CultureInfo.InvariantCulture, $"  descent-override: {source.Descent * 100:0.###}%;\n")
                .Append("  line-gap-override: 0%;\n")
                .Append("  font-display: block;\n")
                .Append("}\n");
        }

        return css.ToString();
    }
}
