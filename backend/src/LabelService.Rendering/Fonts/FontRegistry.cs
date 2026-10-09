using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Fonts;

/// <summary>
/// 字体注册表：服务启动时加载字体目录里的全部字体文件，渲染时按模板的 fontFamily 查找（设计书第 7 节“字体”）。
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
/// 加载完成后只读，线程安全。字体、SKFont 在加载时创建，渲染时不再创建（R3）。
/// </para>
/// </remarks>
public sealed class FontRegistry : IDisposable
{
    /// <summary>
    /// 支持的字体文件扩展名。
    /// </summary>
    public static IReadOnlyList<string> FileExtensions { get; } = [".otf", ".ttf", ".ttc", ".otc"];

    private static readonly string[][] AliasGroups =
    [
        ["SourceHanSansSC", "Source Han Sans SC", "Source Han Sans CN", "Noto Sans SC", "Noto Sans CJK SC", "思源黑体"],
        ["SourceHanSerifSC", "Source Han Serif SC", "Source Han Serif CN", "Noto Serif SC", "Noto Serif CJK SC", "思源宋体"],
    ];

    private static readonly uint WeightAxis = Tag("wght");

    private readonly Dictionary<string, FontFamily> families;
    private readonly List<SKTypeface> owned;
    private readonly FontFace? defaultFallback;

    private FontRegistry(Dictionary<string, FontFamily> families, List<SKTypeface> owned, IReadOnlyList<string> files, IReadOnlyList<string> errors)
    {
        foreach (var family in families.Values)
        {
            family.Seal();
        }

        this.families = families;
        this.owned = owned;
        Files = files;
        Errors = errors;
        defaultFallback = families.Count == 0 ? new FontFace(SKTypeface.Default, fakeBold: false, fakeItalic: false) : null;
    }

    /// <summary>
    /// 没有任何字体的注册表：文本用 Skia 的默认字体，并给 FONT_FALLBACK 警告。只适合不涉及文本的检查和基准。
    /// </summary>
    public static FontRegistry Empty { get; } = new([], [], [], []);

    /// <summary>
    /// 已加载的字体家族名（字体文件里的名字）。
    /// </summary>
    public IReadOnlyCollection<string> FamilyNames => families.Values.Select(family => family.Name).ToArray();

    /// <summary>
    /// 成功加载的字体文件。
    /// </summary>
    public IReadOnlyList<string> Files
    {
        get;
    }

    /// <summary>
    /// 加载失败的文件和原因，由调用方写日志。
    /// </summary>
    public IReadOnlyList<string> Errors
    {
        get;
    }

    /// <summary>
    /// 是否能显示思源黑体（默认字体）。
    /// </summary>
    public bool HasDefaultFamily => FindFamily(TextStyle.DefaultFontFamily) is not null;

    /// <summary>
    /// 加载目录里的全部字体文件（不含子目录）。目录不存在时返回空注册表。只在启动时调用。
    /// </summary>
    public static FontRegistry Load(string directory)
    {
        var families = new Dictionary<string, FontFamily>(StringComparer.Ordinal);
        var owned = new List<SKTypeface>();
        var files = new List<string>();
        var errors = new List<string>();
        if (!Directory.Exists(directory))
        {
            return new FontRegistry(families, owned, files, [$"字体目录不存在：{directory}"]);
        }

        var paths = Directory.EnumerateFiles(directory)
            .Where(path => FileExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            .Order(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var loaded = 0;
            for (var index = 0; ; index++)
            {
                var typeface = SKTypeface.FromFile(path, index);
                if (typeface is null)
                {
                    break;
                }

                owned.Add(typeface);
                AddTypeface(families, owned, typeface);
                loaded++;
            }

            if (loaded == 0)
            {
                errors.Add($"无法读取字体文件：{path}");
            }
            else
            {
                files.Add(path);
            }
        }

        return new FontRegistry(families, owned, files, errors);
    }

    /// <summary>
    /// 释放加载的字体。
    /// </summary>
    public void Dispose()
    {
        foreach (var family in families.Values)
        {
            family.Dispose();
        }

        foreach (var typeface in owned)
        {
            typeface.Dispose();
        }
    }

    /// <summary>
    /// 按模板字体名取字体。找不到时降级到思源黑体并加 FONT_FALLBACK 警告。
    /// </summary>
    internal FontFace Resolve(string? fontFamily, bool bold, bool italic, ICollection<string> warnings)
    {
        var requested = string.IsNullOrWhiteSpace(fontFamily) ? TextStyle.DefaultFontFamily : fontFamily.Trim();
        var family = FindFamily(requested);
        if (family is null)
        {
            warnings.Add(WarningCodes.FontFallback + ":" + Uri.EscapeDataString(requested));
            family = FindFamily(TextStyle.DefaultFontFamily) ?? families.Values.FirstOrDefault();
        }

        return family?.Face(bold, italic) ?? defaultFallback!;
    }

    private FontFamily? FindFamily(string name)
    {
        if (families.TryGetValue(Normalize(name), out var family))
        {
            return family;
        }

        var group = AliasGroups.FirstOrDefault(aliases => aliases.Any(alias => Normalize(alias) == Normalize(name)));
        return group?.Select(alias => families.GetValueOrDefault(Normalize(alias))).FirstOrDefault(found => found is not null);
    }

    private static void AddTypeface(Dictionary<string, FontFamily> families, List<SKTypeface> owned, SKTypeface typeface)
    {
        var key = Normalize(typeface.FamilyName);
        if (!families.TryGetValue(key, out var family))
        {
            family = new FontFamily(typeface.FamilyName);
            families.Add(key, family);
        }

        var axis = typeface.VariationDesignParameters.FirstOrDefault(item => item.Tag == WeightAxis);
        if (axis.Tag != WeightAxis)
        {
            family.Add(typeface, typeface.FontWeight, typeface.IsItalic);
            return;
        }

        // 可变字体：默认实例常常是最细的字重（Noto Sans SC 是 100），按常规 400、粗体 700 各生成一个实例。
        foreach (var weight in new[] { 400f, 700f })
        {
            var value = Math.Clamp(weight, axis.Min, axis.Max);
            var instance = typeface.Clone([new SKFontVariationPositionCoordinate { Axis = WeightAxis, Value = value }]);
            if (instance is not null)
            {
                owned.Add(instance);
                family.Add(instance, (int)value, typeface.IsItalic);
            }
        }
    }

    private static string Normalize(string name) =>
        new(name.Where(ch => ch is not (' ' or '-' or '_')).Select(char.ToLowerInvariant).ToArray());

    private static uint Tag(string tag) => (uint)(tag[0] << 24 | tag[1] << 16 | tag[2] << 8 | tag[3]);
}
