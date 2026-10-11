using System.Buffers.Binary;

namespace LabelService.Rendering.Fonts;

/// <summary>
/// 字体 cmap 表（码位 → 字形）的读写：读出 format 4、format 12 子表的映射，按新映射重新编码整张 cmap。
/// </summary>
/// <remarks>
/// cmap 表开头是编码记录（平台、编码、子表偏移），每条指向一个子表。format 4 是 BMP 的分段映射（最常用），
/// format 12 是覆盖全部平面的分组映射；其他格式（如 format 14 异体字选择器）原样保留。
/// </remarks>
internal static class CmapSubtables
{
    /// <summary>
    /// 读出 cmap 的编码记录。
    /// </summary>
    /// <param name="cmap">cmap 表的数据。</param>
    /// <returns>每条记录的平台、编码、子表偏移和子表格式。</returns>
    public static List<EncodingRecord> ReadEncodingRecords(ReadOnlySpan<byte> cmap)
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

    /// <summary>
    /// 读 format 4 子表（BMP 分段映射），结果合并进 <paramref name="mapping"/>。
    /// </summary>
    /// <param name="table">子表数据（从子表开头起）。</param>
    /// <param name="mapping">码位 → 字形，往里追加。</param>
    public static void ReadFormat4(ReadOnlySpan<byte> table, Dictionary<int, int> mapping)
    {
        // 四个并列数组：段尾、段首、idDelta、idRangeOffset，各 segments 项。
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
                // idRangeOffset 为 0 时字形 = 码位 + idDelta；否则到 glyphIdArray 里取（偏移相对于 idRangeOffset 这一项本身）。
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

    /// <summary>
    /// 读 format 12 子表（全平面分组映射），结果合并进 <paramref name="mapping"/>（覆盖 format 4 读出的同一码位）。
    /// </summary>
    /// <param name="table">子表数据（从子表开头起）。</param>
    /// <param name="mapping">码位 → 字形，往里追加。</param>
    public static void ReadFormat12(ReadOnlySpan<byte> table, Dictionary<int, int> mapping)
    {
        var groups = (int)BinaryPrimitives.ReadUInt32BigEndian(table[12..]);
        for (var g = 0; g < groups; g++)
        {
            // 每组：起始码位、结束码位、起始字形，组内字形随码位递增。
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
    /// <param name="cmap">原 cmap 表的数据。</param>
    /// <param name="records">原 cmap 的编码记录。</param>
    /// <param name="mapping">过滤后的码位 → 字形。</param>
    /// <returns>新的 cmap 表数据。</returns>
    public static byte[] BuildCmap(ReadOnlySpan<byte> cmap, List<EncodingRecord> records, Dictionary<int, int> mapping)
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

        // 编码记录逐条对应到新子表：format 4、12 指向重新编码的，其他格式复制原子表（多条记录共用一个子表时只复制一次）。
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

        // 原来只有 format 4 的字体，补一条 Windows Unicode 全平面（3,10）指向 format 12，免得去掉 format 4 后什么都查不到。
        if (!entries.Any(entry => entry.Subtable == index12))
        {
            entries.Add((3, 10, index12));
        }

        // 编码记录必须按平台、编码排序；子表依次排在记录后面，4 字节对齐。
        entries = entries.Distinct().OrderBy(entry => entry.Platform).ThenBy(entry => entry.Encoding).ToList();
        var headerLength = 4 + entries.Count * 8;
        var offsets = new int[subtables.Count];
        var position = headerLength;
        for (var i = 0; i < subtables.Count; i++)
        {
            offsets[i] = position;
            position += SfntTables.Align(subtables[i].Length);
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

    /// <summary>
    /// 全部映射编码成 format 12：码位连续且字形也连续的合成一组。
    /// </summary>
    /// <param name="mapping">码位 → 字形。</param>
    /// <returns>format 12 子表数据。</returns>
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
    /// <param name="mapping">码位 → 字形。</param>
    /// <returns>format 4 子表数据；放不下时为 null。</returns>
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

        // 规范要求最后一段是 0xFFFF 结尾的哨兵段。
        segments.Add((0xFFFF, 0xFFFF, true));
        var count = segments.Count;
        var arrayLength = segments.Where(segment => !segment.Delta).Sum(segment => segment.End - segment.Start + 1);
        var length = 16 + count * 8 + arrayLength * 2;
        if (length > 0xFFFF)
        {
            return null;
        }

        // 头：格式、长度、segCountX2，以及二分查找用的 searchRange、entrySelector、rangeShift。
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

            // 不规则的段：idRangeOffset 指向 glyphIdArray 里这一段的起点（相对于 idRangeOffset 这一项本身）。
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(ranges + s * 2), (ushort)(array - (ranges + s * 2)));
            for (var code = start; code <= end; code++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(array), (ushort)mapping[code]);
                array += 2;
            }
        }

        return data;
    }

    /// <summary>
    /// 子表的长度：format 0、2、4、6 的长度是 16 位，其余是 32 位（format 14 的长度字段位置不同）。
    /// </summary>
    /// <param name="table">子表数据（从子表开头起）。</param>
    /// <param name="format">子表格式。</param>
    /// <returns>子表的字节数。</returns>
    private static int SubtableLength(ReadOnlySpan<byte> table, ushort format) => format switch
    {
        0 or 2 or 4 or 6 => BinaryPrimitives.ReadUInt16BigEndian(table[2..]),
        _ => (int)BinaryPrimitives.ReadUInt32BigEndian(table[(format == 14 ? 2 : 4)..]),
    };
}

/// <summary>
/// cmap 的一条编码记录。
/// </summary>
/// <param name="Platform">平台 ID（0 Unicode、3 Windows）。</param>
/// <param name="Encoding">编码 ID（如 Windows 的 1 是 BMP、10 是全平面）。</param>
/// <param name="Offset">子表相对于 cmap 表开头的偏移。</param>
/// <param name="Format">子表格式（4、12、14 等）。</param>
internal sealed record EncodingRecord(ushort Platform, ushort Encoding, int Offset, ushort Format);
