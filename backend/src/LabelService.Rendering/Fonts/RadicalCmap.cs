using System.Text;

namespace LabelService.Rendering.Fonts;

/// <summary>
/// 让 PDF 里的中文能正确复制、搜索：去掉字体 cmap 里“和普通汉字共用字形”的部首码位（康熙部首 U+2F00–U+2FDF、部首补充 U+2E80–U+2EFF）。
/// </summary>
/// <remarks>
/// <para>
/// Skia 生成 PDF 时，ToUnicode 是按字体 cmap 反查每个字形对应的字符；一个字形对应多个码位时取码位最小的。
/// 思源 / Noto CJK 里“日”和康熙部首“⽇”（U+2F47）、“寸”和“⼨”（U+2F28）等共用一个字形，部首的码位更小，
/// 于是 PDF 看着对，复制、搜索出来却是部首字符（验收发现：日期 → ⽇期）。
/// </para>
/// <para>
/// 做法：加载字体时把这些部首码位从 cmap 里拿掉（只拿掉和非部首码位共用字形的，独有字形的部首保留），
/// 排版时再用 <see cref="Unify"/> 把文本里真正的部首字符换成对应的汉字（NFKC），显示不受影响。
/// 只改内存里的字体数据，字体文件本身和提供给设计器的 /fonts 不变。字体集合（.ttc）和没有这类部首的字体原样使用。
/// cmap 的读写在 <see cref="CmapSubtables"/>，换表和校验和在 <see cref="SfntTables"/>。
/// </para>
/// </remarks>
internal static class RadicalCmap
{
    /// <summary>
    /// 部首码位 → 对应的汉字码位（只含 NFKC 能换成单个字符的）。
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> Unified = BuildUnified();

    /// <summary>
    /// 部首字符换成对应的汉字（NFKC），其他字符原样返回。
    /// </summary>
    /// <param name="codepoint">码位。</param>
    /// <returns>部首换成的汉字码位，或原码位。</returns>
    public static int Unify(int codepoint) => Unified.TryGetValue(codepoint, out var unified) ? unified : codepoint;

    /// <summary>
    /// 是不是部首区的码位。
    /// </summary>
    /// <param name="codepoint">码位。</param>
    /// <returns>在康熙部首或部首补充区时为 true。</returns>
    public static bool IsRadical(int codepoint) => codepoint is >= 0x2E80 and <= 0x2EFF or >= 0x2F00 and <= 0x2FDF;

    /// <summary>
    /// 过滤字体数据。没有需要拿掉的码位、或者不是单个 sfnt 字体时返回原数组。
    /// </summary>
    /// <param name="font">TrueType / OpenType 字体文件的全部字节。</param>
    /// <param name="removed">拿掉的码位数。</param>
    /// <returns>过滤后的字体数据；不需要过滤时是原数组。</returns>
    public static byte[] Filter(byte[] font, out int removed)
    {
        removed = 0;
        if (font.Length < 12 || Encoding.ASCII.GetString(font, 0, 4) == "ttcf")
        {
            return font;
        }

        var tables = SfntTables.ReadTables(font);
        if (!tables.TryGetValue("cmap", out var cmapRecord))
        {
            return font;
        }

        // 合并 format 4 和 format 12 的映射，得到完整的码位 → 字形。
        var cmap = font.AsSpan(cmapRecord.Offset, cmapRecord.Length);
        var records = CmapSubtables.ReadEncodingRecords(cmap);
        var mapping = new Dictionary<int, int>();
        foreach (var record in records.Where(record => record.Format == 4))
        {
            CmapSubtables.ReadFormat4(cmap[record.Offset..], mapping);
        }

        foreach (var record in records.Where(record => record.Format == 12))
        {
            CmapSubtables.ReadFormat12(cmap[record.Offset..], mapping);
        }

        // 只拿掉“字形也被普通字符用着”的部首码位；部首独有的字形保留，否则这些部首就显示不出来了。
        var shared = mapping.Where(item => !IsRadical(item.Key)).Select(item => item.Value).ToHashSet();
        var drop = mapping.Where(item => IsRadical(item.Key) && item.Value != 0 && shared.Contains(item.Value)).Select(item => item.Key).ToHashSet();
        if (drop.Count == 0)
        {
            return font;
        }

        foreach (var code in drop)
        {
            mapping.Remove(code);
        }

        removed = drop.Count;
        var newCmap = CmapSubtables.BuildCmap(cmap, records, mapping);
        return SfntTables.ReplaceTable(font, tables, "cmap", newCmap);
    }

    /// <summary>
    /// 算出部首 → 汉字的对照表：逐个部首码位做 NFKC，换成的是单个不同字符时记下来。
    /// </summary>
    /// <returns>部首码位 → 汉字码位。</returns>
    private static Dictionary<int, int> BuildUnified()
    {
        var map = new Dictionary<int, int>();
        for (var code = 0x2E80; code <= 0x2FDF; code++)
        {
            var normalized = ((char)code).ToString().Normalize(NormalizationForm.FormKC);
            if (normalized.Length > 0 && char.ConvertToUtf32(normalized, 0) is var unified && unified != code && normalized.Length == char.ConvertFromUtf32(unified).Length)
            {
                map[code] = unified;
            }
        }

        return map;
    }
}
