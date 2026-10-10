using System.Buffers.Binary;
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
/// </para>
/// </remarks>
internal static class RadicalCmap
{
    private static readonly IReadOnlyDictionary<int, int> Unified = BuildUnified();

    /// <summary>
    /// 部首字符换成对应的汉字（NFKC），其他字符原样返回。
    /// </summary>
    public static int Unify(int codepoint) => Unified.TryGetValue(codepoint, out var unified) ? unified : codepoint;

    /// <summary>
    /// 是不是部首区的码位。
    /// </summary>
    public static bool IsRadical(int codepoint) => codepoint is >= 0x2E80 and <= 0x2EFF or >= 0x2F00 and <= 0x2FDF;

    /// <summary>
    /// 过滤字体数据。没有需要拿掉的码位、或者不是单个 sfnt 字体时返回原数组。
    /// </summary>
    /// <param name="font">TrueType / OpenType 字体文件的全部字节。</param>
    /// <param name="removed">拿掉的码位数。</param>
    public static byte[] Filter(byte[] font, out int removed)
    {
        removed = 0;
        if (font.Length < 12 || Encoding.ASCII.GetString(font, 0, 4) == "ttcf")
        {
            return font;
        }

        var tables = ReadTables(font);
        if (!tables.TryGetValue("cmap", out var cmapRecord))
        {
            return font;
        }

        var cmap = font.AsSpan(cmapRecord.Offset, cmapRecord.Length);
        var records = ReadEncodingRecords(cmap);
        var mapping = new Dictionary<int, int>();
        foreach (var record in records.Where(record => record.Format == 4))
        {
            ReadFormat4(cmap[record.Offset..], mapping);
        }

        foreach (var record in records.Where(record => record.Format == 12))
        {
            ReadFormat12(cmap[record.Offset..], mapping);
        }

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
        var newCmap = BuildCmap(cmap, records, mapping);
        return ReplaceTable(font, tables, "cmap", newCmap);
    }

    private sealed record TableRecord(string Tag, int Offset, int Length);

    private sealed record EncodingRecord(ushort Platform, ushort Encoding, int Offset, ushort Format);

    private static Dictionary<string, TableRecord> ReadTables(byte[] font)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        var tables = new Dictionary<string, TableRecord>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var at = 12 + i * 16;
            var tag = Encoding.ASCII.GetString(font, at, 4);
            tables[tag] = new TableRecord(tag, (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(at + 8)), (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(at + 12)));
        }

        return tables;
    }

    private static List<EncodingRecord> ReadEncodingRecords(ReadOnlySpan<byte> cmap)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(cmap[2..]);
        var records = new List<EncodingRecord>(count);
        for (var i = 0; i < count; i++)
        {
            var at = 4 + i * 8;
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(cmap[(at + 4)..]);
            records.Add(new EncodingRecord(BinaryPrimitives.ReadUInt16BigEndian(cmap[at..]), BinaryPrimitives.ReadUInt16BigEndian(cmap[(at + 2)..]), offset, BinaryPrimitives.ReadUInt16BigEndian(cmap[offset..])));
        }

        return records;
    }

    private static void ReadFormat4(ReadOnlySpan<byte> table, Dictionary<int, int> mapping)
    {
        var segments = BinaryPrimitives.ReadUInt16BigEndian(table[6..]) / 2;
        var ends = 14;
        var starts = ends + segments * 2 + 2;
        var deltas = starts + segments * 2;
        var ranges = deltas + segments * 2;
        for (var s = 0; s < segments; s++)
        {
            int end = BinaryPrimitives.ReadUInt16BigEndian(table[(ends + s * 2)..]);
            int start = BinaryPrimitives.ReadUInt16BigEndian(table[(starts + s * 2)..]);
            int delta = BinaryPrimitives.ReadInt16BigEndian(table[(deltas + s * 2)..]);
            int rangeOffset = BinaryPrimitives.ReadUInt16BigEndian(table[(ranges + s * 2)..]);
            for (var code = start; code <= end && code != 0xFFFF; code++)
            {
                int glyph;
                if (rangeOffset == 0)
                {
                    glyph = (code + delta) & 0xFFFF;
                }
                else
                {
                    var at = ranges + s * 2 + rangeOffset + (code - start) * 2;
                    glyph = at + 2 <= table.Length ? BinaryPrimitives.ReadUInt16BigEndian(table[at..]) : 0;
                    glyph = glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
                }

                if (glyph != 0)
                {
                    mapping[code] = glyph;
                }
            }
        }
    }

    private static void ReadFormat12(ReadOnlySpan<byte> table, Dictionary<int, int> mapping)
    {
        var groups = (int)BinaryPrimitives.ReadUInt32BigEndian(table[12..]);
        for (var g = 0; g < groups; g++)
        {
            var at = 16 + g * 12;
            var start = (int)BinaryPrimitives.ReadUInt32BigEndian(table[at..]);
            var end = (int)BinaryPrimitives.ReadUInt32BigEndian(table[(at + 4)..]);
            var glyph = (int)BinaryPrimitives.ReadUInt32BigEndian(table[(at + 8)..]);
            for (var code = start; code <= end; code++)
            {
                mapping[code] = glyph + (code - start);
            }
        }
    }

    /// <summary>
    /// 重建 cmap：format 4、format 12 按过滤后的映射重新编码，其他子表（format 14 等）原样保留。
    /// format 4 超过 64 KB 放不下时去掉它，并保证有 (3,10) format 12。
    /// </summary>
    private static byte[] BuildCmap(ReadOnlySpan<byte> cmap, List<EncodingRecord> records, Dictionary<int, int> mapping)
    {
        var format12 = Format12(mapping);
        var format4 = Format4(mapping);
        var subtables = new List<byte[]>();
        var entries = new List<(ushort Platform, ushort Encoding, int Subtable)>();
        var copied = new Dictionary<int, int>();
        int Add(byte[] data)
        {
            subtables.Add(data);
            return subtables.Count - 1;
        }

        int? index4 = format4 is null ? null : Add(format4);
        var index12 = Add(format12);
        foreach (var record in records)
        {
            switch (record.Format)
            {
                case 4 when index4 is { } i4:
                    entries.Add((record.Platform, record.Encoding, i4));
                    break;
                case 4:
                    break;
                case 12:
                    entries.Add((record.Platform, record.Encoding, index12));
                    break;
                default:
                    if (!copied.TryGetValue(record.Offset, out var index))
                    {
                        index = Add(cmap[record.Offset..(record.Offset + SubtableLength(cmap[record.Offset..], record.Format))].ToArray());
                        copied[record.Offset] = index;
                    }

                    entries.Add((record.Platform, record.Encoding, index));
                    break;
            }
        }

        if (!entries.Any(entry => entry.Subtable == index12))
        {
            entries.Add((3, 10, index12));
        }

        entries = entries.Distinct().OrderBy(entry => entry.Platform).ThenBy(entry => entry.Encoding).ToList();
        var headerLength = 4 + entries.Count * 8;
        var offsets = new int[subtables.Count];
        var position = headerLength;
        for (var i = 0; i < subtables.Count; i++)
        {
            offsets[i] = position;
            position += Align(subtables[i].Length);
        }

        var result = new byte[position];
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(2), (ushort)entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var at = 4 + i * 8;
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(at), entries[i].Platform);
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(at + 2), entries[i].Encoding);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(at + 4), (uint)offsets[entries[i].Subtable]);
        }

        for (var i = 0; i < subtables.Count; i++)
        {
            subtables[i].CopyTo(result, offsets[i]);
        }

        return result;
    }

    private static byte[] Format12(Dictionary<int, int> mapping)
    {
        var groups = new List<(int Start, int End, int Glyph)>();
        foreach (var code in mapping.Keys.Order())
        {
            var glyph = mapping[code];
            if (groups.Count > 0 && groups[^1].End == code - 1 && groups[^1].Glyph + (code - groups[^1].Start) == glyph)
            {
                groups[^1] = groups[^1] with
                {
                    End = code
                };
            }
            else
            {
                groups.Add((code, code, glyph));
            }
        }

        var data = new byte[16 + groups.Count * 12];
        BinaryPrimitives.WriteUInt16BigEndian(data, 12);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12), (uint)groups.Count);
        for (var i = 0; i < groups.Count; i++)
        {
            var at = 16 + i * 12;
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), (uint)groups[i].Start);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at + 4), (uint)groups[i].End);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at + 8), (uint)groups[i].Glyph);
        }

        return data;
    }

    /// <summary>
    /// BMP 部分编码成 format 4：连续码位一段；整段“字形 = 码位 + 常数”时用 idDelta，否则用 glyphIdArray。超过 64 KB 返回 null。
    /// </summary>
    private static byte[]? Format4(Dictionary<int, int> mapping)
    {
        var codes = mapping.Keys.Where(code => code < 0xFFFF).Order().ToList();
        var segments = new List<(int Start, int End, bool Delta)>();
        foreach (var code in codes)
        {
            if (segments.Count > 0 && segments[^1].End == code - 1)
            {
                var segment = segments[^1];
                var delta = segment.Delta && mapping[code] - code == mapping[segment.Start] - segment.Start;
                segments[^1] = (segment.Start, code, delta);
            }
            else
            {
                segments.Add((code, code, true));
            }
        }

        segments.Add((0xFFFF, 0xFFFF, true));
        var count = segments.Count;
        var arrayLength = segments.Where(segment => !segment.Delta).Sum(segment => segment.End - segment.Start + 1);
        var length = 16 + count * 8 + arrayLength * 2;
        if (length > 0xFFFF)
        {
            return null;
        }

        var data = new byte[length];
        var search = 1 << (int)Math.Floor(Math.Log2(count));
        BinaryPrimitives.WriteUInt16BigEndian(data, 4);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), (ushort)length);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(6), (ushort)(count * 2));
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(8), (ushort)(search * 2));
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(10), (ushort)Math.Log2(search));
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(12), (ushort)((count - search) * 2));
        var ends = 14;
        var starts = ends + count * 2 + 2;
        var deltas = starts + count * 2;
        var ranges = deltas + count * 2;
        var array = ranges + count * 2;
        for (var s = 0; s < count; s++)
        {
            var (start, end, delta) = segments[s];
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(ends + s * 2), (ushort)end);
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(starts + s * 2), (ushort)start);
            if (start == 0xFFFF)
            {
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(deltas + s * 2), 1);
                continue;
            }

            if (delta)
            {
                BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(deltas + s * 2), unchecked((short)(mapping[start] - start)));
                continue;
            }

            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(ranges + s * 2), (ushort)(array - (ranges + s * 2)));
            for (var code = start; code <= end; code++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(array), (ushort)mapping[code]);
                array += 2;
            }
        }

        return data;
    }

    private static int SubtableLength(ReadOnlySpan<byte> table, ushort format) => format switch
    {
        0 or 2 or 4 or 6 => BinaryPrimitives.ReadUInt16BigEndian(table[2..]),
        _ => (int)BinaryPrimitives.ReadUInt32BigEndian(table[(format == 14 ? 2 : 4)..]),
    };

    /// <summary>
    /// 换掉一个表，重排偏移（4 字节对齐），重算表校验和与 head.checkSumAdjustment。
    /// </summary>
    private static byte[] ReplaceTable(byte[] font, Dictionary<string, TableRecord> tables, string tag, byte[] replacement)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        var order = Enumerable.Range(0, count).Select(i => Encoding.ASCII.GetString(font, 12 + i * 16, 4)).ToList();
        var dataStart = 12 + count * 16;
        var position = dataStart;
        var layout = new Dictionary<string, (int Offset, int Length)>(StringComparer.Ordinal);
        foreach (var name in tables.Values.OrderBy(table => table.Offset).Select(table => table.Tag))
        {
            var length = name == tag ? replacement.Length : tables[name].Length;
            layout[name] = (position, length);
            position += Align(length);
        }

        var result = new byte[position];
        font.AsSpan(0, 12).CopyTo(result);
        foreach (var name in layout.Keys)
        {
            var source = name == tag ? replacement : font.AsSpan(tables[name].Offset, tables[name].Length).ToArray();
            source.CopyTo(result, layout[name].Offset);
        }

        if (layout.TryGetValue("head", out var head) && head.Length >= 12)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(head.Offset + 8), 0);
        }

        for (var i = 0; i < count; i++)
        {
            var name = order[i];
            var at = 12 + i * 16;
            Encoding.ASCII.GetBytes(name).CopyTo(result, at);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(at + 4), Checksum(result.AsSpan(layout[name].Offset, Align(layout[name].Length))));
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(at + 8), (uint)layout[name].Offset);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(at + 12), (uint)layout[name].Length);
        }

        if (layout.TryGetValue("head", out head) && head.Length >= 12)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(head.Offset + 8), unchecked(0xB1B0AFBA - Checksum(result)));
        }

        return result;
    }

    private static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        var i = 0;
        for (; i + 4 <= data.Length; i += 4)
        {
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(data[i..]));
        }

        if (i < data.Length)
        {
            Span<byte> tail = stackalloc byte[4];
            data[i..].CopyTo(tail);
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(tail));
        }

        return sum;
    }

    private static int Align(int length) => (length + 3) & ~3;

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
