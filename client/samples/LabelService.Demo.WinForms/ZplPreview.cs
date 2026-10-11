using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace LabelService.Demo;

/// <summary>
/// 把标签服务生成的 ZPL（每张一个 ^GFA 位图）还原成黑白图片，在“预览”里显示打印效果。
/// </summary>
/// <remarks>
/// 只认服务端输出的写法：<c>^PW</c>、<c>^LL</c>、<c>^GFA,总字节,总字节,每行字节,数据</c>。数据支持 ZPL 的 ASCII 压缩：
/// G–Y 表示重复 1–19 次，g–z 表示 20–400 次，<c>,</c> 行尾补 0，<c>!</c> 行尾补 1，<c>:</c> 重复上一行。
/// </remarks>
internal static class ZplPreview
{
    /// <summary>
    /// 一张标签：^XA 到 ^XZ 之间的内容。
    /// </summary>
    private static readonly Regex Label = new Regex(@"\^XA(?<body>.*?)\^XZ", RegexOptions.Singleline);

    /// <summary>
    /// ^GFA 图形指令：总字节数、每行字节数和数据。
    /// </summary>
    private static readonly Regex Graphic = new Regex(@"\^GFA,(?<total>\d+),\d+,(?<row>\d+),(?<data>[^\^]*)", RegexOptions.Singleline);

    /// <summary>
    /// 解析出每张标签的位图（最多 <paramref name="max"/> 张）。认不出的标签跳过。
    /// </summary>
    /// <param name="zpl">ZPL 文本，可以有多张（每张一个 ^XA…^XZ）。</param>
    /// <param name="max">最多解析几张。</param>
    /// <returns>黑白位图，由调用方释放。</returns>
    public static List<Bitmap> Render(string zpl, int max)
    {
        var result = new List<Bitmap>();
        foreach (Match label in Label.Matches(zpl))
        {
            if (result.Count >= max)
            {
                break;
            }

            var graphic = Graphic.Match(label.Groups["body"].Value);
            if (!graphic.Success)
            {
                continue;
            }

            var bytesPerRow = int.Parse(graphic.Groups["row"].Value, CultureInfo.InvariantCulture);
            var total = int.Parse(graphic.Groups["total"].Value, CultureInfo.InvariantCulture);
            var width = Number(label.Groups["body"].Value, "^PW") ?? bytesPerRow * 8;
            var rows = Decode(graphic.Groups["data"].Value, bytesPerRow, total / Math.Max(1, bytesPerRow));
            result.Add(ToBitmap(rows, Math.Min(width, bytesPerRow * 8), bytesPerRow));
        }

        return result;
    }

    /// <summary>
    /// 解压 ^GFA 的 ASCII 数据，返回每行的字节。
    /// </summary>
    /// <param name="data">^GFA 的数据部分。</param>
    /// <param name="bytesPerRow">每行字节数。</param>
    /// <param name="height">行数，解够了就停。</param>
    /// <returns>每行的字节，1 位是黑点。</returns>
    internal static List<byte[]> Decode(string data, int bytesPerRow, int height)
    {
        var rows = new List<byte[]>(height);
        var hexPerRow = bytesPerRow * 2;
        var row = new char[hexPerRow];
        var position = 0;
        var repeat = 0;
        void Flush(char fill)
        {
            for (; position < hexPerRow; position++)
            {
                row[position] = fill;
            }

            var bytes = new byte[bytesPerRow];
            for (var i = 0; i < bytesPerRow; i++)
            {
                bytes[i] = byte.Parse(new string(row, i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            rows.Add(bytes);
            position = 0;
        }

        foreach (var ch in data)
        {
            if (rows.Count >= height)
            {
                break;
            }

            if (ch >= 'G' && ch <= 'Y')
            {
                repeat += ch - 'G' + 1;
            }
            else if (ch >= 'g' && ch <= 'z')
            {
                repeat += (ch - 'g' + 1) * 20;
            }
            else if (ch == ',')
            {
                Flush('0');
            }
            else if (ch == '!')
            {
                Flush('F');
            }
            else if (ch == ':')
            {
                rows.Add(rows.Count > 0 ? (byte[])rows[rows.Count - 1].Clone() : new byte[bytesPerRow]);
            }
            else if (Uri.IsHexDigit(ch))
            {
                for (var i = Math.Max(1, repeat); i > 0 && position < hexPerRow; i--)
                {
                    row[position++] = char.ToUpperInvariant(ch);
                }

                repeat = 0;
                if (position == hexPerRow)
                {
                    Flush('0');
                }
            }
        }

        return rows;
    }

    /// <summary>
    /// 把每行的字节画成黑白位图（直接写像素内存，比逐点 SetPixel 快得多）。
    /// </summary>
    /// <param name="rows">每行的字节。</param>
    /// <param name="width">宽度，像素。</param>
    /// <param name="bytesPerRow">每行字节数。</param>
    /// <returns>位图。</returns>
    private static Bitmap ToBitmap(List<byte[]> rows, int width, int bytesPerRow)
    {
        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, rows.Count), PixelFormat.Format32bppRgb);
        var locked = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        var pixels = new int[locked.Stride / 4 * bitmap.Height];
        for (var y = 0; y < rows.Count; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var black = x / 8 < bytesPerRow && (rows[y][x / 8] & (0x80 >> (x % 8))) != 0;
                pixels[y * (locked.Stride / 4) + x] = black ? unchecked((int)0xFF000000) : unchecked((int)0xFFFFFFFF);
            }
        }

        Marshal.Copy(pixels, 0, locked.Scan0, pixels.Length);
        bitmap.UnlockBits(locked);
        return bitmap;
    }

    /// <summary>
    /// 取 ZPL 指令后面的数字，如 ^PW 后的宽度。
    /// </summary>
    /// <param name="text">一张标签的 ZPL。</param>
    /// <param name="command">指令，如 ^PW。</param>
    /// <returns>数字；没有这个指令时为 null。</returns>
    private static int? Number(string text, string command)
    {
        var match = Regex.Match(text, Regex.Escape(command) + @"(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }
}
