using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Rendering.Fonts;

/// <summary>
/// 字体注册表：加载部署目录和上传目录里的全部字体文件，渲染时按模板的 fontFamily 查找（设计书第 7 节“字体”，ADR 0011）。
/// </summary>
/// <remarks>
/// <para>
/// 模板字体名和字体文件里的家族名按“忽略大小写、空格、连字符、下划线”比较。思源黑体和 Noto Sans SC 是同一款字体
/// （Adobe 和 Google 的两个发行名），所以 SourceHanSansSC 也认 Noto Sans SC、Noto Sans CJK SC；思源宋体同理。
/// </para>
/// <para>
/// 找不到时降级到思源黑体，并给 FONT_FALLBACK 警告。一个字体都没加载到时用 Skia 的默认字体，中文会显示成方框。
/// </para>
/// <para>
/// 线程安全。字体、SKFont 在加载时创建，渲染时不再创建（R3）。管理后台上传、删除字体后调用 <see cref="Reload"/>：
/// 整份换成新加载的一套，渲染时取一次当前的那套；旧的一套不立即释放（可能还有请求在用），等注册表释放时一起释放。
/// </para>
/// </remarks>
public sealed class FontRegistry : IDisposable
{
    /// <summary>
    /// 支持的字体文件扩展名。
    /// </summary>
    public static IReadOnlyList<string> FileExtensions { get; } = [".otf", ".ttf", ".ttc", ".otc"];

    /// <summary>
    /// 同一款字体的不同名字，每组第一个是模板里写的名字（和设计器一致）。
    /// </summary>
    internal static readonly string[][] AliasGroups =
    [
        ["SourceHanSansSC", "Source Han Sans SC", "Source Han Sans CN", "Noto Sans SC", "Noto Sans CJK SC", "思源黑体"],
        ["SourceHanSerifSC", "Source Han Serif SC", "Source Han Serif CN", "Noto Serif SC", "Noto Serif CJK SC", "思源宋体"],
    ];

    /// <summary>
    /// 可变字体的字重轴标签 wght。
    /// </summary>
    internal static readonly uint WeightAxis = Tag("wght");

    /// <summary>
    /// 内置别名的模板字体名（每组别名的第一个）：思源黑体、思源宋体。
    /// </summary>
    public static IReadOnlyList<string> TemplateFamilies { get; } = AliasGroups.Select(group => group[0]).ToArray();

    /// <summary>
    /// 部署目录；<see cref="Empty"/> 为 null（不能重新加载）。
    /// </summary>
    private readonly string? directory;

    /// <summary>
    /// 换下来的旧字体，等注册表释放时一起释放。
    /// </summary>
    private readonly List<FontSnapshot> retired = [];

    /// <summary>
    /// 重新加载时加锁，避免两次上传同时换。
    /// </summary>
    private readonly Lock reloadGate = new();

    /// <summary>
    /// 当前的一套字体。渲染时读一次引用就用这一套，重新加载时整份替换。
    /// </summary>
    private volatile FontSnapshot current;

    /// <summary>
    /// 由 <see cref="Load(string, string?)"/> 创建。
    /// </summary>
    /// <param name="snapshot">加载好的一套字体。</param>
    /// <param name="directory">部署目录；null 表示不能重新加载。</param>
    /// <param name="uploadDirectory">上传目录；null 表示没有。</param>
    private FontRegistry(FontSnapshot snapshot, string? directory, string? uploadDirectory)
    {
        current = snapshot;
        this.directory = directory;
        UploadDirectory = uploadDirectory;
    }

    /// <summary>
    /// 没有任何字体的注册表：文本用 Skia 的默认字体，并给 FONT_FALLBACK 警告。只适合不涉及文本的检查和基准。
    /// </summary>
    public static FontRegistry Empty { get; } = new(FontSnapshot.Empty, null, null);

    /// <summary>
    /// 上传字体的目录（数据目录的 fonts/）；null 表示不支持上传（检查程序、基准）。
    /// </summary>
    public string? UploadDirectory
    {
        get;
    }

    /// <summary>
    /// 已加载的字体家族名（字体文件里的名字）。
    /// </summary>
    public IReadOnlyCollection<string> FamilyNames => current.Families.Values.Select(family => family.Name).ToArray();

    /// <summary>
    /// 模板里可以写的字体名：内置别名的字体用模板名（SourceHanSansSC 等），其余用字体文件里的家族名。内置的在前，其余按名字排序。
    /// </summary>
    public IReadOnlyList<string> TemplateNames
    {
        get
        {
            var names = current.Families.Values.Select(family => TemplateNameOf(family.Name)).Distinct(StringComparer.Ordinal).ToList();
            var builtin = TemplateFamilies.Where(names.Contains);
            return builtin.Concat(names.Except(TemplateFamilies).Order(StringComparer.Ordinal)).ToArray();
        }
    }

    /// <summary>
    /// 成功加载的字体文件。
    /// </summary>
    public IReadOnlyList<string> Files => current.Files;

    /// <summary>
    /// 每个成功加载的字体文件：家族名、大小、修改时间、是否上传的。
    /// </summary>
    public IReadOnlyList<FontFileInfo> FileDetails => current.Details;

    /// <summary>
    /// 加载失败的文件和原因，由调用方写日志。
    /// </summary>
    public IReadOnlyList<string> Errors => current.Errors;

    /// <summary>
    /// 是否能显示思源黑体（默认字体）。
    /// </summary>
    public bool HasDefaultFamily => current.FindFamily(TextStyle.DefaultFontFamily) is not null;

    /// <summary>
    /// 加载目录里的全部字体文件（不含子目录），不支持上传。目录不存在时返回空注册表。
    /// </summary>
    /// <param name="directory">字体目录。</param>
    /// <returns>字体注册表；加载失败的文件记在 <see cref="Errors"/> 里。</returns>
    public static FontRegistry Load(string directory) => Load(directory, null);

    /// <summary>
    /// 加载部署目录和上传目录里的全部字体文件（不含子目录）。
    /// </summary>
    /// <param name="directory">部署目录（程序目录的 fonts/）。</param>
    /// <param name="uploadDirectory">上传目录（数据目录的 fonts/）；null 表示不支持上传。</param>
    /// <returns>字体注册表；加载失败的文件记在 <see cref="Errors"/> 里。</returns>
    public static FontRegistry Load(string directory, string? uploadDirectory) =>
        new(FontSnapshot.Load(directory, uploadDirectory), directory, uploadDirectory);

    /// <summary>
    /// 读出字体文件内容里的家族名，用来检查上传的文件是不是字体。
    /// </summary>
    /// <param name="content">文件内容。</param>
    /// <returns>家族名；不是字体时为空。</returns>
    public static IReadOnlyList<string> ReadFamilyNames(byte[] content) => FontSnapshot.ReadFamilyNames(content);

    /// <summary>
    /// 重新加载两个目录里的字体（上传、删除字体后调用），立即生效。旧的一套等注册表释放时再释放。
    /// </summary>
    public void Reload()
    {
        if (directory is null)
        {
            return;
        }

        lock (reloadGate)
        {
            var next = FontSnapshot.Load(directory, UploadDirectory);
            retired.Add(current);
            current = next;
        }
    }

    /// <summary>
    /// 模板字体名对应的字体文件：和渲染时找到的是同一个家族（同样认别名），但不做降级，找不到返回 null。
    /// 设计器通过 /fonts 加载同一份文件，保证所见即所得。一个家族有多个文件时取第一个加载的（部署目录优先，按文件名排序）。
    /// </summary>
    /// <param name="fontFamily">模板里的字体名，如 SourceHanSansSC。</param>
    /// <returns>字体文件、是否可变字体和上下高度；找不到时为 null。</returns>
    public FontSource? SourceOf(string fontFamily)
    {
        var snapshot = current;
        var family = snapshot.FindFamily(fontFamily);
        if (family is null || !snapshot.Sources.TryGetValue(FontSnapshot.Normalize(family.Name), out var source))
        {
            return null;
        }

        var face = family.Face(bold: false, italic: false);
        return new FontSource(source.Path, source.IsVariable, face.Ascent, face.Descent);
    }

    /// <summary>
    /// 释放加载的字体（当前的和换下来的）。
    /// </summary>
    public void Dispose()
    {
        lock (reloadGate)
        {
            current.Dispose();
            foreach (var snapshot in retired)
            {
                snapshot.Dispose();
            }

            retired.Clear();
        }
    }

    /// <summary>
    /// 按模板字体名取字体。找不到时降级到思源黑体并加 FONT_FALLBACK 警告。
    /// </summary>
    /// <param name="fontFamily">模板里的字体名；空时用默认字体（思源黑体）。</param>
    /// <param name="bold">是否粗体。</param>
    /// <param name="italic">是否斜体。</param>
    /// <param name="warnings">渲染警告，降级时往里加 FONT_FALLBACK。</param>
    /// <returns>要用的字体。</returns>
    internal FontFace Resolve(string? fontFamily, bool bold, bool italic, ICollection<string> warnings)
    {
        // 只读一次当前的那套字体，这次查找和降级都用它（重新加载换掉也不影响）。
        var snapshot = current;
        var requested = string.IsNullOrWhiteSpace(fontFamily) ? TextStyle.DefaultFontFamily : fontFamily.Trim();
        var family = snapshot.FindFamily(requested);
        if (family is not null)
        {
            RenderCaches.Fonts.Hit();
        }
        else
        {
            RenderCaches.Fonts.Miss();
            warnings.Add(WarningCodes.FontFallback + ":" + Uri.EscapeDataString(requested));
            family = snapshot.FindFamily(TextStyle.DefaultFontFamily) ?? snapshot.Families.Values.FirstOrDefault();
        }

        return family?.Face(bold, italic) ?? snapshot.DefaultFallback!;
    }

    /// <summary>
    /// 字体文件里的家族名对应的模板字体名：属于内置别名组时用组里第一个名字，否则原样。
    /// </summary>
    /// <param name="familyName">字体文件里的家族名，如 Noto Sans SC。</param>
    /// <returns>如 SourceHanSansSC。</returns>
    private static string TemplateNameOf(string familyName)
    {
        var key = FontSnapshot.Normalize(familyName);
        return AliasGroups.FirstOrDefault(group => group.Any(alias => FontSnapshot.Normalize(alias) == key))?[0] ?? familyName;
    }

    /// <summary>
    /// 四个字符的 OpenType 标签转成 32 位整数。
    /// </summary>
    /// <param name="tag">如 wght。</param>
    /// <returns>大端拼成的整数。</returns>
    private static uint Tag(string tag) => (uint)(tag[0] << 24 | tag[1] << 16 | tag[2] << 8 | tag[3]);
}
