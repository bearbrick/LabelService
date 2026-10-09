using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// ZPL 输出和黑白化（T-006）：结构（^XA…^XZ、^PW、^LL、^PQ）、ASCII 压缩按 ZPL II 手册解码后和黑白 PNG 逐像素一致、PNG 的 mono。
/// </summary>
/// <remarks>
/// 解码器按手册规则独立实现（不调用产品代码的压缩函数），并先用手册里的例子“vMB = 327 个 B”核对计数字母。
/// </remarks>
internal static partial class ZplChecks
{
    public static void Run(string backendRoot, Action<string, bool> check)
    {
        check("ZPL 解码器按手册：vMB 是 327 个 B，G–Y 是 1–19，g–z 是 20–400",
            Expand("vMB") == new string('B', 327) && Expand("GA") == "A" && Expand("YA") == new string('A', 19) && Expand("zA") == new string('A', 400));

        var renderer = Fixtures.Renderer(backendRoot);
        var template = Fixtures.LoadTemplate(backendRoot);
        var values = FieldValidator.Validate(template.Fields, null, Fixtures.Data(Fixtures.BoxLabelData)).Values;
        RenderResult Render(LabelFormat format, bool mono, params LabelInstance[] labels) =>
            renderer.Render(new RenderJob { Template = template, Labels = labels, Format = format, Dpi = 203, Mono = mono });

        var zplResult = Render(LabelFormat.Zpl, false, new LabelInstance(values, 3));
        var zpl = Encoding.ASCII.GetString(zplResult.Content.Span);
        check("ZPL 是纯文本，Content-Type 是 text/plain", zplResult.ContentType.StartsWith("text/plain", StringComparison.Ordinal) && zpl.All(ch => ch < 128));
        check("一张标签一个 ^XA…^XZ，^PW799 ^LL480（100×60mm @203dpi），份数用 ^PQ3",
            Regex.Count(zpl, @"\^XA") == 1 && Regex.Count(zpl, @"\^XZ") == 1 && zpl.Contains("^PW799^LL480", StringComparison.Ordinal) && zpl.Contains("^PQ3", StringComparison.Ordinal));

        var graphic = Graphic().Match(zpl);
        var total = int.Parse(graphic.Groups["total"].Value, CultureInfo.InvariantCulture);
        var bytesPerRow = int.Parse(graphic.Groups["row"].Value, CultureInfo.InvariantCulture);
        var decoded = Decode(graphic.Groups["data"].Value, total, bytesPerRow);
        using var png = SKBitmap.Decode(Render(LabelFormat.Png, true, new LabelInstance(values)).Content.ToArray());
        var mismatched = 0;
        for (var y = 0; y < png.Height; y++)
        {
            for (var x = 0; x < png.Width; x++)
            {
                var black = (decoded[y * bytesPerRow + x / 8] & (0x80 >> (x % 8))) != 0;
                if (black != (png.GetPixel(x, y).Red < 128))
                {
                    mismatched++;
                }
            }
        }

        check($"^GFA 按手册解码后和黑白 PNG 逐像素一致（{bytesPerRow} 字节/行 × {png.Height} 行，不一致 {mismatched} 点）",
            graphic.Success && total == bytesPerRow * png.Height && bytesPerRow == (png.Width + 7) / 8 && mismatched == 0);
        check($"ASCII 压缩生效：{graphic.Groups["data"].Length} 个字符，不压缩要 {total * 2} 个", graphic.Groups["data"].Length < total * 2 / 3);
        check("黑白 PNG 只有纯黑和纯白", Enumerable.Range(0, png.Height).All(y => Enumerable.Range(0, png.Width).All(x => png.GetPixel(x, y).Red is 0 or 255)));

        var batch = Encoding.ASCII.GetString(Render(LabelFormat.Zpl, false, new LabelInstance(values), new LabelInstance(values, 2)).Content.Span);
        check("多张标签：每张一个 ^XA…^XZ，各自的份数", Regex.Count(batch, @"\^XA") == 2 && batch.Contains("^PQ1", StringComparison.Ordinal) && batch.Contains("^PQ2", StringComparison.Ordinal));
    }

    /// <summary>
    /// 把一段计数字母 + 字符展开，用来核对手册里的例子。
    /// </summary>
    private static string Expand(string text)
    {
        var count = 0;
        var result = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch is >= 'G' and <= 'Y')
            {
                count += ch - 'G' + 1;
            }
            else if (ch is >= 'g' and <= 'z')
            {
                count += (ch - 'g' + 1) * 20;
            }
            else
            {
                result.Append(ch, Math.Max(1, count));
                count = 0;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 按 ZPL II 手册解 ^GFA 的 ASCII 压缩：计数字母、逗号（本行剩余补 0）、感叹号（补 F）、冒号（重复上一行）。
    /// </summary>
    private static byte[] Decode(string data, int total, int bytesPerRow)
    {
        var width = bytesPerRow * 2;
        var hex = new StringBuilder(total * 2);
        var row = new StringBuilder(width);
        var previous = new string('0', width);
        var count = 0;
        void Flush()
        {
            previous = row.ToString();
            hex.Append(previous);
            row.Clear();
        }

        foreach (var ch in data)
        {
            if (ch is >= 'G' and <= 'Y')
            {
                count += ch - 'G' + 1;
            }
            else if (ch is >= 'g' and <= 'z')
            {
                count += (ch - 'g' + 1) * 20;
            }
            else if (ch == ',')
            {
                row.Append('0', width - row.Length);
                Flush();
            }
            else if (ch == '!')
            {
                row.Append('F', width - row.Length);
                Flush();
            }
            else if (ch == ':')
            {
                row.Clear().Append(previous);
                Flush();
            }
            else if (Uri.IsHexDigit(ch))
            {
                row.Append(ch, Math.Max(1, count));
                count = 0;
                if (row.Length >= width)
                {
                    Flush();
                }
            }
        }

        if (row.Length > 0)
        {
            Flush();
        }

        var text = hex.ToString();
        return Enumerable.Range(0, Math.Min(total, text.Length / 2)).Select(i => Convert.ToByte(text.Substring(i * 2, 2), 16)).ToArray();
    }

    [GeneratedRegex(@"\^GFA,(?<total>\d+),\d+,(?<row>\d+),(?<data>[^\^]*)\^FS")]
    private static partial Regex Graphic();
}
