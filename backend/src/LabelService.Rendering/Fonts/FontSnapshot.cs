using SkiaSharp;

namespace LabelService.Rendering.Fonts;

/// <summary>
/// 某一时刻加载好的一整套字体（部署目录加上传目录）。加载后只读，线程安全；
/// <see cref="FontRegistry"/> 重新加载时整份换成新的一套，旧的一套等注册表释放时再释放（正在渲染的请求还在用）。
/// </summary>
internal sealed class FontSnapshot : IDisposable
{
    /// <summary>
    /// 由 <see cref="Load"/> 创建：封好每个家族的四种组合。
    /// </summary>
    /// <param name="families">字体家族。</param>
    /// <param name="owned">要负责释放的字形。</param>
    /// <param name="files">成功加载的字体文件。</param>
    /// <param name="errors">加载失败的文件和原因。</param>
    /// <param name="sources">家族名 → 字体文件。</param>
    /// <param name="details">每个字体文件的家族名、大小、来源。</param>
    private FontSnapshot(
        Dictionary<string, FontFamily> families,
        List<SKTypeface> owned,
        IReadOnlyList<string> files,
        IReadOnlyList<string> errors,
        Dictionary<string, (string Path, bool IsVariable)> sources,
        IReadOnlyList<FontFileInfo> details)
    {
        foreach (var family in families.Values)
        {
            family.Seal();
        }

        Families = families;
        Owned = owned;
        Sources = sources;
        Files = files;
        Errors = errors;
        Details = details;
        DefaultFallback = families.Count == 0 ? new FontFace(SKTypeface.Default, fakeBold: false, fakeItalic: false) : null;
    }

    /// <summary>
    /// 一个字体都没有的一套：文本用 Skia 的默认字体。
    /// </summary>
    public static FontSnapshot Empty { get; } = new([], [], [], [], [], []);

    /// <summary>
    /// 规范化的家族名（去掉空格、连字符、下划线，小写）→ 字体家族。
    /// </summary>
    public Dictionary<string, FontFamily> Families
    {
        get;
    }

    /// <summary>
    /// 加载时创建的全部字形（包括可变字体生成的实例），释放时一起释放。
    /// </summary>
    public List<SKTypeface> Owned
    {
        get;
    }

    /// <summary>
    /// 规范化的家族名 → 第一个提供它的字体文件和是否可变字体（给设计器的 /fonts 用；部署目录优先）。
    /// </summary>
    public Dictionary<string, (string Path, bool IsVariable)> Sources
    {
        get;
    }

    /// <summary>
    /// 成功加载的字体文件（绝对路径）。
    /// </summary>
    public IReadOnlyList<string> Files
    {
        get;
    }

    /// <summary>
    /// 加载失败的文件和原因。
    /// </summary>
    public IReadOnlyList<string> Errors
    {
        get;
    }

    /// <summary>
    /// 每个成功加载的字体文件：家族名、大小、修改时间、是否上传的。
    /// </summary>
    public IReadOnlyList<FontFileInfo> Details
    {
        get;
    }

    /// <summary>
    /// 一个字体都没加载到时用的 Skia 默认字体；有字体时为 null。
    /// </summary>
    public FontFace? DefaultFallback
    {
        get;
    }

    /// <summary>
    /// 加载部署目录和上传目录里的全部字体文件（不含子目录）。部署目录不存在时记一条错误；上传目录不存在表示还没上传过。
    /// 上传的字体整份读进内存，不占着文件，删除时不会因为文件被占用删不掉。
    /// </summary>
    /// <param name="directory">部署目录。</param>
    /// <param name="uploadDirectory">上传目录；null 表示没有。</param>
    /// <returns>加载好的一套字体。</returns>
    public static FontSnapshot Load(string directory, string? uploadDirectory)
    {
        var families = new Dictionary<string, FontFamily>(StringComparer.Ordinal);
        var owned = new List<SKTypeface>();
        var files = new List<string>();
        var errors = new List<string>();
        var sources = new Dictionary<string, (string Path, bool IsVariable)>(StringComparer.Ordinal);
        var details = new List<FontFileInfo>();
        if (!Directory.Exists(directory))
        {
            errors.Add($"字体目录不存在：{directory}");
        }

        foreach (var (folder, uploaded) in new[] { (directory, false), (uploadDirectory, true) })
        {
            if (folder is null || !Directory.Exists(folder))
            {
                continue;
            }

            var paths = Directory.EnumerateFiles(folder)
                .Where(path => FontRegistry.FileExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .Order(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var names = LoadFile(path, uploaded, families, owned, sources);
                if (names.Count == 0)
                {
                    errors.Add($"无法读取字体文件：{path}");
                    continue;
                }

                files.Add(path);
                var info = new FileInfo(path);
                details.Add(new FontFileInfo(path, names, info.Length, info.LastWriteTimeUtc, uploaded));
            }
        }

        return new FontSnapshot(families, owned, files, errors, sources, details);
    }

    /// <summary>
    /// 读出字体文件内容里的家族名，用来检查上传的文件是不是字体。不加载进注册表。
    /// </summary>
    /// <param name="content">文件内容。</param>
    /// <returns>家族名（去重）；不是字体时为空。</returns>
    public static IReadOnlyList<string> ReadFamilyNames(byte[] content)
    {
        var names = new List<string>();
        using var data = SKData.CreateCopy(content);
        for (var index = 0; ; index++)
        {
            using var typeface = SKTypeface.FromData(data, index);
            if (typeface is null)
            {
                break;
            }

            names.Add(typeface.FamilyName);
        }

        return names.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// 按名字找字体家族：先按名字本身找，找不到再按别名组里的其他名字找。
    /// </summary>
    /// <param name="name">字体名。</param>
    /// <returns>字体家族；找不到时为 null。</returns>
    public FontFamily? FindFamily(string name)
    {
        if (Families.TryGetValue(Normalize(name), out var family))
        {
            return family;
        }

        var group = FontRegistry.AliasGroups.FirstOrDefault(aliases => aliases.Any(alias => Normalize(alias) == Normalize(name)));
        return group?.Select(alias => Families.GetValueOrDefault(Normalize(alias))).FirstOrDefault(found => found is not null);
    }

    /// <summary>
    /// 释放这一套字体。
    /// </summary>
    public void Dispose()
    {
        foreach (var family in Families.Values)
        {
            family.Dispose();
        }

        foreach (var typeface in Owned)
        {
            typeface.Dispose();
        }
    }

    /// <summary>
    /// 字体名的比较形式：去掉空格、连字符、下划线，转小写。
    /// </summary>
    /// <param name="name">字体名。</param>
    /// <returns>如 Source Han Sans SC → sourcehansanssc。</returns>
    public static string Normalize(string name) =>
        new(name.Where(ch => ch is not (' ' or '-' or '_')).Select(char.ToLowerInvariant).ToArray());

    /// <summary>
    /// 加载一个字体文件里的全部字形。一个文件可能是字体集合（.ttc），按序号一个个读，直到读不出来为止。
    /// </summary>
    /// <param name="path">字体文件。</param>
    /// <param name="inMemory">是否整份读进内存（上传的字体）；否则不需要过滤的字体按文件加载。</param>
    /// <param name="families">字体家族，读到的字形加进去。</param>
    /// <param name="owned">要负责释放的字形。</param>
    /// <param name="sources">家族名 → 字体文件。</param>
    /// <returns>这个文件里的家族名；读不出来时为空。</returns>
    private static List<string> LoadFile(
        string path,
        bool inMemory,
        Dictionary<string, FontFamily> families,
        List<SKTypeface> owned,
        Dictionary<string, (string Path, bool IsVariable)> sources)
    {
        var names = new List<string>();
        using var data = LoadData(path, inMemory);
        for (var index = 0; ; index++)
        {
            var typeface = data is null ? SKTypeface.FromFile(path, index) : SKTypeface.FromData(data, index);
            if (typeface is null)
            {
                break;
            }

            owned.Add(typeface);
            AddTypeface(families, owned, typeface);
            sources.TryAdd(Normalize(typeface.FamilyName), (path, typeface.VariationDesignParameters.Any(item => item.Tag == FontRegistry.WeightAxis)));
            if (!names.Contains(typeface.FamilyName, StringComparer.Ordinal))
            {
                names.Add(typeface.FamilyName);
            }
        }

        return names;
    }

    /// <summary>
    /// 单个字体文件按 <see cref="RadicalCmap"/> 去掉和汉字共用字形的部首码位后加载（PDF 复制、搜索出来的字才对）；
    /// 字体集合和不需要改的字体返回 null，照常按文件加载；要求整份读进内存时总是返回数据。
    /// </summary>
    /// <param name="path">字体文件。</param>
    /// <param name="inMemory">是否总是读进内存。</param>
    /// <returns>字体数据；按文件加载时为 null。</returns>
    private static SKData? LoadData(string path, bool inMemory)
    {
        var bytes = File.ReadAllBytes(path);
        var filtered = RadicalCmap.Filter(bytes, out var removed);
        return removed == 0 && !inMemory ? null : SKData.CreateCopy(filtered);
    }

    /// <summary>
    /// 把一个字形加进对应的家族；可变字体按常规、粗体两个字重各生成一个实例。
    /// </summary>
    /// <param name="families">字体家族，按规范化的名字索引。</param>
    /// <param name="owned">要负责释放的字形，生成的实例也加进去。</param>
    /// <param name="typeface">字形。</param>
    private static void AddTypeface(Dictionary<string, FontFamily> families, List<SKTypeface> owned, SKTypeface typeface)
    {
        var key = Normalize(typeface.FamilyName);
        if (!families.TryGetValue(key, out var family))
        {
            family = new FontFamily(typeface.FamilyName);
            families.Add(key, family);
        }

        var axis = typeface.VariationDesignParameters.FirstOrDefault(item => item.Tag == FontRegistry.WeightAxis);
        if (axis.Tag != FontRegistry.WeightAxis)
        {
            family.Add(typeface, typeface.FontWeight, typeface.IsItalic);
            return;
        }

        // 可变字体：默认实例常常是最细的字重（Noto Sans SC 是 100），按常规 400、粗体 700 各生成一个实例。
        foreach (var weight in new[] { 400f, 700f })
        {
            var value = Math.Clamp(weight, axis.Min, axis.Max);
            var instance = typeface.Clone([new SKFontVariationPositionCoordinate { Axis = FontRegistry.WeightAxis, Value = value }]);
            if (instance is not null)
            {
                owned.Add(instance);
                family.Add(instance, (int)value, typeface.IsItalic);
            }
        }
    }
}
