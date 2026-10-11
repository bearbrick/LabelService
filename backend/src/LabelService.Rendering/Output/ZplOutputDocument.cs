using System.Globalization;
using System.Text;
using LabelService.Contracts.Protocol;
using SkiaSharp;

namespace LabelService.Rendering.Output;

/// <summary>
/// ZPL：整张标签栅格化成黑白位图，用 ^GFA 输出，斑马等支持 ZPL II 的标签机可以直接打印（设计书第 7 节）。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>每张标签一个 <c>^XA … ^XZ</c>：<c>^PW</c>、<c>^LL</c> 是点数，份数用 <c>^PQ</c>。</item>
/// <item>位图按行转成十六进制（查表），再用 ZPL 自带的 ASCII 压缩：连续相同字符写成“次数字母 + 字符”
/// （G–Y 表示 1–19，g–z 表示 20–400），行尾全是 0 写 <c>,</c>，全是 F 写 <c>!</c>，和上一行相同写 <c>:</c>。</item>
/// <item>不用 <c>:Z64:</c>：它要算 CRC，各型号对 CRC 的要求不一，没有打印机实测前不用。ASCII 压缩对标签这种大片空白的图已经足够小。</item>
/// </list>
/// </remarks>
/// <param name="widthDots">标签宽度（打印点，写进 ^PW）。</param>
/// <param name="heightDots">标签高度（打印点，写进 ^LL）。</param>
/// <param name="dpi">分辨率（应和打印机一致）。</param>
internal sealed class ZplOutputDocument(int widthDots, int heightDots, int dpi) : OutputDocument(widthDots, heightDots, dpi)
{
    /// <summary>
    /// 字节 → 两位大写十六进制的查表。
    /// </summary>
    private static readonly string[] Hex = Enumerable.Range(0, 256).Select(value => value.ToString("X2", CultureInfo.InvariantCulture)).ToArray();

    /// <summary>
    /// 已写出的 ZPL，每张标签追加一段 ^XA … ^XZ。
    /// </summary>
    private readonly StringBuilder output = new();

    /// <summary>
    /// 标签的位图，多张标签共用一张（每张开始时清成白底）。
    /// </summary>
    private SKBitmap? bitmap;

    /// <summary>
    /// 位图上的画布。
    /// </summary>
    private SKCanvas? canvas;

    /// <inheritdoc />
    public override LabelFormat Format => LabelFormat.Zpl;

    /// <inheritdoc />
    public override SKCanvas BeginLabel()
    {
        bitmap ??= new SKBitmap(new SKImageInfo(WidthDots, HeightDots, SKColorType.Rgba8888, SKAlphaType.Opaque));
        canvas ??= new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        return canvas;
    }

    /// <inheritdoc />
    public override void EndLabel(int copies)
    {
        if (bitmap is null || canvas is null)
        {
            throw new InvalidOperationException("没有正在绘制的标签。");
        }

        canvas.Flush();
        var packed = Monochrome.Pack(bitmap, out var bytesPerRow);
        // ^GFA,总字节数,图形字节数,每行字节数,数据：整张标签作为一个图形放在原点。
        var total = packed.Length.ToString(CultureInfo.InvariantCulture);
        output.Append("^XA^PW").Append(WidthDots).Append("^LL").Append(HeightDots).Append("^LH0,0\n");
        output.Append("^FO0,0^GFA,").Append(total).Append(',').Append(total).Append(',').Append(bytesPerRow).Append(',');
        Compress(packed, bytesPerRow, output);
        output.Append("^FS\n^PQ").Append(copies).Append("\n^XZ\n");
    }

    /// <inheritdoc />
    public override void WriteTo(Stream stream)
    {
        // ZPL 只有 ASCII 字符，UTF-8 和 ASCII 相同。
        var bytes = Encoding.ASCII.GetBytes(output.ToString());
        stream.Write(bytes);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        canvas?.Dispose();
        bitmap?.Dispose();
    }

    /// <summary>
    /// 按 ZPL II 手册的 ASCII 压缩规则写出位图数据。
    /// </summary>
    /// <param name="packed">1 位位图，按行存放，1 为黑。</param>
    /// <param name="bytesPerRow">每行的字节数。</param>
    /// <param name="output">输出，往里追加。</param>
    internal static void Compress(byte[] packed, int bytesPerRow, StringBuilder output)
    {
        var rows = packed.Length / bytesPerRow;
        var row = new char[bytesPerRow * 2];
        var previous = new char[row.Length];
        for (var y = 0; y < rows; y++)
        {
            for (var i = 0; i < bytesPerRow; i++)
            {
                var hex = Hex[packed[y * bytesPerRow + i]];
                row[i * 2] = hex[0];
                row[i * 2 + 1] = hex[1];
            }

            // 和上一行完全相同的行只写一个冒号。
            if (y > 0 && row.AsSpan().SequenceEqual(previous))
            {
                output.Append(':');
                continue;
            }

            CompressRow(row, output);
            (previous, row) = (row, previous);
        }
    }

    /// <summary>
    /// 压缩一行十六进制字符：重复字符写成“次数字母 + 字符”，行尾的 0 或 F 用逗号、感叹号补满。
    /// </summary>
    /// <param name="row">一行的十六进制字符。</param>
    /// <param name="output">输出，往里追加。</param>
    private static void CompressRow(char[] row, StringBuilder output)
    {
        // 行尾连续的 0 或 F 用 , 或 ! 一次补满。
        var end = row.Length;
        char? fill = null;
        foreach (var candidate in new[] { '0', 'F' })
        {
            var tail = end;
            while (tail > 0 && row[tail - 1] == candidate)
            {
                tail--;
            }

            if (tail < end && (fill is null || tail < end))
            {
                fill = candidate;
                end = tail;
                break;
            }
        }

        for (var i = 0; i < end;)
        {
            var run = 1;
            while (i + run < end && row[i + run] == row[i])
            {
                run++;
            }

            AppendRun(row[i], run, output);
            i += run;
        }

        if (fill is { } value)
        {
            output.Append(value == '0' ? ',' : '!');
        }
    }

    /// <summary>
    /// 写一段重复字符：次数 = 小写字母（20 的倍数，最多 400）+ 大写字母（1–19），次数为 1 时只写字符本身。
    /// </summary>
    /// <param name="value">重复的字符。</param>
    /// <param name="count">重复次数。</param>
    /// <param name="output">输出，往里追加。</param>
    private static void AppendRun(char value, int count, StringBuilder output)
    {
        while (count > 0)
        {
            // 一组最多表示 419 次（z = 400 加 Y = 19），超过时分成几组。
            var chunk = Math.Min(count, 419);
            if (chunk > 1)
            {
                if (chunk >= 20)
                {
                    output.Append((char)('g' + chunk / 20 - 1));
                }

                if (chunk % 20 > 0)
                {
                    output.Append((char)('G' + chunk % 20 - 1));
                }
            }

            output.Append(value);
            count -= chunk;
        }
    }
}
