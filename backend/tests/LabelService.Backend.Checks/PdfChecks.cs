using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Validation;

namespace LabelService.Backend.Checks;

/// <summary>
/// PDF 输出（T-005）：页数（份数、多张）、页面尺寸、字体嵌入、文字可选中（ToUnicode 里有中文）。
/// 不引入 PDF 解析库，按 PDF 的文本结构核对，压缩流用 ZLibStream 解开。
/// </summary>
internal static partial class PdfChecks
{
    public static void Run(string backendRoot, Action<string, bool> check)
    {
        var renderer = Fixtures.Renderer(backendRoot);
        var template = Fixtures.LoadTemplate(backendRoot);
        var values = FieldValidator.Validate(template.Fields, null, Fixtures.Data(Fixtures.BoxLabelData)).Values;
        check("渲染引擎支持 PDF", renderer.Supports(LabelFormat.Pdf));

        var single = renderer.Render(new RenderJob { Template = template, Labels = [new LabelInstance(values, 3)], Format = LabelFormat.Pdf, Dpi = 300 });
        var bytes = single.Content.ToArray();
        var text = Encoding.Latin1.GetString(bytes);
        check("输出是完整的 PDF，Content-Type 是 application/pdf",
            text.StartsWith("%PDF-", StringComparison.Ordinal) && text.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal) && single.ContentType == "application/pdf");
        check("copies=3 输出 3 页，张数是 3", PageCount(text) == 3 && single.LabelCount == 3);

        var box = MediaBox().Match(text);
        var width = double.Parse(box.Groups["w"].Value, CultureInfo.InvariantCulture);
        var height = double.Parse(box.Groups["h"].Value, CultureInfo.InvariantCulture);
        // Skia 按 RasterDpi 把页面对齐到打印点：和 mm 换算的尺寸相差不到半个点（300dpi 下 0.12 pt）。
        var halfDot = 72.0 / 300 / 2;
        check($"页面尺寸等于标签尺寸 100×60mm，误差小于半个打印点（{width}×{height} pt）",
            box.Success && Math.Abs(width - 100 / LabelUnits.MmPerInch * 72) <= halfDot && Math.Abs(height - 60 / LabelUnits.MmPerInch * 72) <= halfDot);
        check("示例箱标的 PDF 全部是矢量（黑色图标不栅格化）", !text.Contains("/Subtype /Image", StringComparison.Ordinal));

        var streams = Inflate(bytes);
        check("字体嵌入 PDF（不依赖打印机或电脑上装的字体）", text.Contains("/Type3", StringComparison.Ordinal) || text.Contains("/FontFile", StringComparison.Ordinal));
        check("文字是矢量、可选中：ToUnicode 里有“成”“品”和数字（数字是区间映射，从 0030 开始）", text.Contains("/ToUnicode", StringComparison.Ordinal)
            && streams.Contains("<6210>", StringComparison.OrdinalIgnoreCase) && streams.Contains("<54C1>", StringComparison.OrdinalIgnoreCase)
            && streams.Contains("<0030>", StringComparison.OrdinalIgnoreCase));

        var batch = renderer.Render(new RenderJob
        {
            Template = template,
            Labels = [new LabelInstance(values), new LabelInstance(values, 2)],
            Format = LabelFormat.Pdf,
            Dpi = 203,
        });
        check("多张标签合成一个 PDF：每张每份一页", PageCount(Encoding.Latin1.GetString(batch.Content.Span)) == 3 && batch.LabelCount == 3);
        CheckToUnicode(backendRoot, renderer, bytes, check);
    }

    /// <summary>
    /// 复制、搜索出来的字要对（T-021）：思源 / Noto 里“日”“寸”“毛”和康熙部首共用字形，ToUnicode 不能映射成部首（⽇ U+2F47 等）。
    /// 按 PDF 文本结构解出全部 ToUnicode 的 bfchar / bfrange，核对映射到的字符，不只看某个字有没有出现。
    /// </summary>
    private static void CheckToUnicode(string backendRoot, LabelRenderer renderer, byte[] boxPdf, Action<string, bool> check)
    {
        var box = ToUnicodeTargets(Inflate(boxPdf));
        check("箱标 PDF 的 ToUnicode：“日期”的“日”映射到 U+65E5，没有任何字形映射到部首区（U+2E80–U+2FDF）",
            box.Contains(0x65E5) && !box.Any(IsRadical));

        var a4 = Fixtures.LoadTemplate(backendRoot, "a4-shipping-mark.json");
        var a4Values = FieldValidator.Validate(a4.Fields, null, a4.Fields.Where(field => field.Sample is not null).ToDictionary(field => field.Key, field => field.Sample!.Value)).Values;
        var a4Pdf = renderer.Render(new RenderJob { Template = a4, Labels = [new LabelInstance(a4Values)], Format = LabelFormat.Pdf, Dpi = 300 });
        var a4Targets = ToUnicodeTargets(Inflate(a4Pdf.Content.ToArray()));
        check("唛头 PDF 的 ToUnicode：“尺寸”的“寸” U+5BF8、“毛重”的“毛” U+6BDB 正确，没有部首",
            a4Targets.Contains(0x5BF8) && a4Targets.Contains(0x6BDB) && !a4Targets.Any(IsRadical));

        // 文本里真正的部首字符（⽇ U+2F47）按对应的汉字显示和提取，画出来和“日”一样。
        LabelTemplate Text(string content) => new()
        {
            Code = "RADICAL",
            Page = new LabelPage { Width = 30, Height = 12, Dpi = 300 },
            Elements = [new TextElement { Id = "t", X = 1, Y = 1, Width = 28, Height = 10, Content = content, Style = new TextStyle { FontSize = 20 } }],
        };
        RenderResult Draw(string content, LabelFormat format) =>
            renderer.Render(new RenderJob { Template = Text(content), Labels = [new LabelInstance(new Dictionary<string, object?>())], Format = format, Dpi = 300 });
        var radicalTargets = ToUnicodeTargets(Inflate(Draw("⽇期", LabelFormat.Pdf).Content.ToArray()));
        check("文本里的康熙部首“⽇”照常显示（和“日”画出来一样），PDF 里提取成“日”",
            Draw("⽇期", LabelFormat.Png).Content.Span.SequenceEqual(Draw("日期", LabelFormat.Png).Content.Span)
            && radicalTargets.Contains(0x65E5) && !radicalTargets.Any(IsRadical));
    }

    private static bool IsRadical(int codepoint) => codepoint is >= 0x2E80 and <= 0x2FDF;

    /// <summary>
    /// 解出 ToUnicode CMap 里全部映射到的字符（bfchar 和 bfrange，目标是 UTF-16BE 十六进制）。
    /// </summary>
    private static HashSet<int> ToUnicodeTargets(string inflated)
    {
        var targets = new HashSet<int>();
        void Add(string hex, int offset)
        {
            var units = Enumerable.Range(0, hex.Length / 4).Select(i => Convert.ToInt32(hex.Substring(i * 4, 4), 16)).ToArray();
            units[^1] += offset;
            var text = new string(units.Select(unit => (char)unit).ToArray());
            for (var i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
            {
                targets.Add(char.ConvertToUtf32(text, i));
            }
        }

        foreach (Match block in BfChar().Matches(inflated))
        {
            foreach (Match pair in HexPair().Matches(block.Groups[1].Value))
            {
                Add(pair.Groups[2].Value, 0);
            }
        }

        foreach (Match block in BfRange().Matches(inflated))
        {
            foreach (Match range in HexRange().Matches(block.Groups[1].Value))
            {
                var low = Convert.ToInt32(range.Groups[1].Value, 16);
                var high = Convert.ToInt32(range.Groups[2].Value, 16);
                if (range.Groups[3].Success)
                {
                    for (var code = low; code <= high; code++)
                    {
                        Add(range.Groups[3].Value, code - low);
                    }
                }
                else
                {
                    foreach (Match item in HexItem().Matches(range.Groups[4].Value))
                    {
                        Add(item.Groups[1].Value, 0);
                    }
                }
            }
        }

        return targets;
    }

    private static int PageCount(string pdf) => PageObject().Count(pdf);

    /// <summary>
    /// 解开全部 FlateDecode 流，拼成一个字符串（Latin1），用来找 ToUnicode 映射。
    /// </summary>
    private static string Inflate(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var result = new StringBuilder();
        foreach (Match match in StreamStart().Matches(text))
        {
            var start = match.Index + match.Length;
            var end = text.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0)
            {
                continue;
            }

            try
            {
                using var input = new MemoryStream(pdf, start, end - start);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                result.Append(Encoding.Latin1.GetString(output.ToArray()));
            }
            catch (InvalidDataException)
            {
                // 不是 zlib 流（如图片的其他编码），跳过。
            }
        }

        return result.ToString();
    }

    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PageObject();

    [GeneratedRegex(@"/MediaBox\s*\[\s*0\s+0\s+(?<w>[\d.]+)\s+(?<h>[\d.]+)\s*\]")]
    private static partial Regex MediaBox();

    [GeneratedRegex(@"stream\r?\n")]
    private static partial Regex StreamStart();

    [GeneratedRegex(@"beginbfchar(.*?)endbfchar", RegexOptions.Singleline)]
    private static partial Regex BfChar();

    [GeneratedRegex(@"beginbfrange(.*?)endbfrange", RegexOptions.Singleline)]
    private static partial Regex BfRange();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")]
    private static partial Regex HexPair();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(?:<([0-9A-Fa-f]+)>|\[([^\]]*)\])")]
    private static partial Regex HexRange();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>")]
    private static partial Regex HexItem();
}
