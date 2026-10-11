using System.Buffers.Binary;
using System.Text;

namespace LabelService.Rendering.Fonts;

/// <summary>
/// TrueType / OpenType（sfnt）字体文件的表目录：读出每张表的位置，换掉其中一张表并重算校验和。
/// </summary>
/// <remarks>
/// 文件开头 12 字节是头（第 4–5 字节是表的个数），后面每张表一条 16 字节的记录：标签、校验和、偏移、长度。
/// 表的数据按 4 字节对齐存放；head 表里的 checkSumAdjustment 让整个文件的校验和等于 0xB1B0AFBA。
/// </remarks>
internal static class SfntTables
{
    /// <summary>
    /// 读出表目录。
    /// </summary>
    /// <param name="font">字体文件的全部字节（单个字体，不是 .ttc 集合）。</param>
    /// <returns>表标签（如 cmap、head）→ 表的位置。</returns>
    public static Dictionary<string, TableRecord> ReadTables(byte[] font)
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

    /// <summary>
    /// 换掉一个表，重排偏移（4 字节对齐），重算表校验和与 head.checkSumAdjustment。
    /// </summary>
    /// <param name="font">原字体文件。</param>
    /// <param name="tables">原字体的表目录。</param>
    /// <param name="tag">要换掉的表，如 cmap。</param>
    /// <param name="replacement">新的表数据。</param>
    /// <returns>新的字体文件。</returns>
    public static byte[] ReplaceTable(byte[] font, Dictionary<string, TableRecord> tables, string tag, byte[] replacement)
    {
        // 表数据按原来的先后顺序重新排放，只有被换掉的表长度变了。
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

        // 算表校验和之前 head.checkSumAdjustment 要先清零（规范要求）。
        if (layout.TryGetValue("head", out var head) && head.Length >= 12)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(head.Offset + 8), 0);
        }

        // 表目录按原来的顺序写回，偏移、长度、校验和换成新的。
        for (var i = 0; i < count; i++)
        {
            var name = order[i];
            var at = 12 + i * 16;
            Encoding.ASCII.GetBytes(name).CopyTo(result, at);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(at + 4), Checksum(result.AsSpan(layout[name].Offset, Align(layout[name].Length))));
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(at + 8), (uint)layout[name].Offset);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(at + 12), (uint)layout[name].Length);
        }

        // 最后让整个文件的校验和等于规范规定的常数。
        if (layout.TryGetValue("head", out head) && head.Length >= 12)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(head.Offset + 8), unchecked(0xB1B0AFBA - Checksum(result)));
        }

        return result;
    }

    /// <summary>
    /// 长度按 4 字节向上对齐。
    /// </summary>
    /// <param name="length">长度。</param>
    /// <returns>不小于 <paramref name="length"/> 的 4 的倍数。</returns>
    public static int Align(int length) => (length + 3) & ~3;

    /// <summary>
    /// sfnt 的校验和：按大端 32 位整数累加（不足 4 字节的尾部补零）。
    /// </summary>
    /// <param name="data">数据。</param>
    /// <returns>校验和。</returns>
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
}

/// <summary>
/// 表目录里的一张表。
/// </summary>
/// <param name="Tag">表标签，如 cmap、head。</param>
/// <param name="Offset">表数据在文件里的偏移。</param>
/// <param name="Length">表数据的长度（不含对齐补的零）。</param>
internal sealed record TableRecord(string Tag, int Offset, int Length);
