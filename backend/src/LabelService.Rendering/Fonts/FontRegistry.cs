using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Diagnostics;
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

    /// <summary>
    /// 设计器提供的模板字体名（每组别名的第一个）：思源黑体、思源宋体。
    /// </summary>
    public static IReadOnlyList<string> TemplateFamilies { get; } = AliasGroups.Select(group => group[0]).ToArray();

    private readonly Dictionary<string, FontFamily> families;
    private readonly List<SKTypeface> owned;
    private readonly FontFace? defaultFallback;
    private readonly Dictionary<string, (string Path, bool IsVariable)> sources;

    private FontRegistry(
        Dictionary<string, FontFamily> families,
        List<SKTypeface> owned,
        IReadOnlyList<string> files,
        IReadOnlyList<string> errors,
        Dictionary<string, (string Path, bool IsVariable)> sources)
    {
        foreach (var family in families.Values)
        {
            family.Seal();
        }

        this.families = families;
        this.owned = owned;
        this.sources = sources;
        Files = files;
        Errors = errors;
        defaultFallback = families.Count == 0 ? new FontFace(SKTypeface.Default, fakeBold: false, fakeItalic: false) : null;
    }

    /// <summary>
    /// 没有任何字体的注册表：文本用 Skia 的默认字体，并给 FONT_FALLBACK 警告。只适合不涉及文本的检查和基准。
    /// </summary>
    public static FontRegistry Empty { get; } = new([], [], [], [], []);

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
        var sources = new Dictionary<string, (string Path, bool IsVariable)>(StringComparer.Ordinal);
        if (!Directory.Exists(directory))
        {
            return new FontRegistry(families, owned, files, [$"字体目录不存在：{directory}"], sources);
        }

        var paths = Directory.EnumerateFiles(directory)
            .Where(path => FileExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            .Order(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var loaded = 0;
            using var data = LoadData(path);
            for (var index = 0; ; index++)
            {
                var typeface = data is null ? SKTypeface.FromFile(path, index) : SKTypeface.FromData(data, index);
                if (typeface is null)
                {
                    break;
                }

                owned.Add(typeface);
                AddTypeface(families, owned, typeface);
                sources.TryAdd(Normalize(typeface.FamilyName), (path, typeface.VariationDesignParameters.Any(item => item.Tag == WeightAxis)));
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

        return new FontRegistry(families, owned, files, errors, sources);
    }

    /// <summary>
    /// 模板字体名对应的字体文件：和渲染时找到的是同一个家族（同样认别名），但不做降级，找不到返回 null。
    /// 设计器通过 /fonts 加载同一份文件，保证所见即所得。一个家族有多个文件时取第一个加载的（按文件名排序）。
    /// </summary>
    public FontSource? SourceOf(string fontFamily)
    {
        var family = FindFamily(fontFamily);
        if (family is null || !sources.TryGetValue(Normalize(family.Name), out var source))
        {
            return null;
        }

        var face = family.Face(bold: false, italic: false);
        return new FontSource(source.Path, source.IsVariable, face.Ascent, face.Descent);
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
    /// 单个字体文件按 <see cref="RadicalCmap"/> 去掉和汉字共用字形的部首码位后加载（PDF 复制、搜索出来的字才对）；
    /// 字体集合和不需要改的字体返回 null，照常按文件加载。
    /// </summary>
    private static SKData? LoadData(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var filtered = RadicalCmap.Filter(bytes, out var removed);
        return removed == 0 ? null : SKData.CreateCopy(filtered);
    }

    /// <summary>
    /// 按模板字体名取字体。找不到时降级到思源黑体并加 FONT_FALLBACK 警告。
    /// </summary>
    internal FontFace Resolve(string? fontFamily, bool bold, bool italic, ICollection<string> warnings)
    {
        var requested = string.IsNullOrWhiteSpace(fontFamily) ? TextStyle.DefaultFontFamily : fontFamily.Trim();
        var family = FindFamily(requested);
        if (family is not null)
        {
            RenderCaches.Fonts.Hit();
        }
        else
        {
            RenderCaches.Fonts.Miss();
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
