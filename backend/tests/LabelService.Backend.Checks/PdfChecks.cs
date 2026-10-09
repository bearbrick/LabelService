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
}
