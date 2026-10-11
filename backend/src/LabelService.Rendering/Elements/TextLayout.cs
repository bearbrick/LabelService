using System.Text;
using LabelService.Contracts;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Fonts;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 文本排版：测量、换行、自动缩小、对齐（设计书第 5 节“文本排版规则”）。只做计算，不画。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>fit=none：不自动换行，超出框的部分裁掉；fit=wrap：在框宽内换行，中文按字、英文按单词，行首不放结束标点、行尾不放开始标点，
/// 一个单词比框还宽时按字符断开，超出框高的部分裁掉；fit=shrink：不自动换行，放不下时缩小字号，最小到 minFontSize（默认 4pt）。</item>
/// <item>内容里的换行符在三种模式下都换行（和设计器一致）。</item>
/// <item>行高 = 字号 × lineHeight；字间距加在相邻两个字之间；每行的字在行高内垂直居中（字体的 ascent、descent 居中，和浏览器的 middle 基线一致）。</item>
/// </list>
/// 先按参考字号测一次字宽，换算到不同字号只做乘法，所以 shrink 的二分查找不需要重复测量。分行规则在 <see cref="LineBreaking"/>。
/// </remarks>
internal static class TextLayout
{
    /// <summary>
    /// shrink 的最小字号默认值（pt），和设计器一致。
    /// </summary>
    public const double DefaultMinFontSize = 4;

    /// <summary>
    /// 二分查找的次数，和设计器一致。
    /// </summary>
    private const int ShrinkIterations = 14;

    /// <summary>
    /// 排版一段文本。坐标单位是打印点，原点在元素框左上角。
    /// </summary>
    /// <param name="text">已替换绑定的文本。</param>
    /// <param name="style">样式；null 时全部取默认值。</param>
    /// <param name="face">字体。</param>
    /// <param name="width">框宽（打印点）。</param>
    /// <param name="height">框高（打印点）。</param>
    /// <param name="dpi">分辨率，用来把 pt、mm 换算成打印点。</param>
    /// <returns>实际字号、行高、各行字形的位置，以及是否有内容被裁掉。</returns>
    public static TextBlock Arrange(string text, TextStyle? style, FontFace face, float width, float height, int dpi)
    {
        // 按参考字号测一次字宽，之后不同字号只做乘法。
        style ??= new TextStyle();
        var runes = Runes(text);
        var codepoints = runes.Select(rune => rune.Value).ToArray();
        var glyphs = new ushort[codepoints.Length];
        var advances = new float[codepoints.Length];
        face.Measure(codepoints, glyphs, advances);

        var fontSizePt = style.FontSize is > 0 ? style.FontSize.Value : TextStyle.DefaultFontSize;
        var lineHeight = (float)(style.LineHeight is > 0 ? style.LineHeight.Value : TextStyle.DefaultLineHeight);
        var spacing = (float)LabelUnits.MmToDots(style.LetterSpacing ?? 0, dpi);
        var fit = style.Fit ?? TextFit.None;
        var fitTolerance = (float)LabelUnits.MmToDots(0.001, dpi);
        var clipTolerance = (float)LabelUnits.MmToDots(0.05, dpi);
        var measured = new MeasuredText(runes, advances, spacing);

        List<TextSpan> LinesAt(float size) => fit == TextFit.Wrap ? LineBreaking.Wrap(measured, size, width) : LineBreaking.Paragraphs(measured);
        bool Fits(float size)
        {
            var lines = LinesAt(size);
            return lines.Max(line => measured.Width(line, size)) <= width + fitTolerance
                && lines.Count * size * lineHeight <= height + fitTolerance;
        }

        // shrink：原字号放不下时，在 [最小字号, 原字号] 之间二分查找能放下的最大字号；最小字号也放不下就用最小字号（超出部分裁掉）。
        var size = Dots(fontSizePt, dpi);
        if (fit == TextFit.Shrink && runes.Length > 0 && !Fits(size))
        {
            // 下限不小于 1mm，和设计器一致。
            var minPt = Math.Max(style.MinFontSize ?? DefaultMinFontSize, LabelUnits.PointsPerInch / LabelUnits.MmPerInch);
            var low = Dots(Math.Min(minPt, fontSizePt), dpi);
            var high = size;
            if (Fits(low))
            {
                for (var i = 0; i < ShrinkIterations; i++)
                {
                    var middle = (low + high) / 2;
                    if (Fits(middle))
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }
            }

            size = low;
        }

        // 垂直对齐：整块文字在框里靠上、居中或靠下；每行的字在行高内按 ascent、descent 居中。
        var spans = runes.Length == 0 ? [] : LinesAt(size);
        var lineBox = size * lineHeight;
        var total = spans.Count * lineBox;
        var top = (style.VAlign ?? VerticalAlign.Middle) switch
        {
            VerticalAlign.Top => 0,
            VerticalAlign.Bottom => height - total,
            _ => (height - total) / 2,
        };
        var baselineOffset = (lineBox - (face.Ascent + face.Descent) * size) / 2 + face.Ascent * size;
        var lines = new List<TextLine>(spans.Count);
        var widest = 0f;
        for (var index = 0; index < spans.Count; index++)
        {
            // 水平对齐：按这一行的宽度算左端，再逐字累加横坐标。
            var span = spans[index];
            var lineWidth = measured.Width(span, size);
            widest = Math.Max(widest, lineWidth);
            var left = (style.Align ?? TextAlign.Left) switch
            {
                TextAlign.Center => (width - lineWidth) / 2,
                TextAlign.Right => width - lineWidth,
                _ => 0,
            };
            var x = new float[span.Length];
            var cursor = left;
            for (var i = 0; i < span.Length; i++)
            {
                x[i] = cursor;
                cursor += advances[span.Start + i] * size + spacing;
            }

            var lineTop = top + index * lineBox;
            lines.Add(new TextLine(glyphs.AsSpan(span.Start, span.Length).ToArray(), x, left, lineWidth, lineTop, lineTop + baselineOffset));
        }

        // 超出框 0.05mm 以上才算被裁掉（浮点误差不算），由绘制器给 TEXT_CLIPPED 警告。
        var clipped = widest > width + clipTolerance || total > height + clipTolerance;
        return new TextBlock(size, lineBox, lines, clipped);
    }

    /// <summary>
    /// 把文本切成 Unicode 字符（代理对算一个字）。回车统一成换行，制表符当空格，其他控制字符去掉。
    /// </summary>
    /// <param name="text">文本。</param>
    /// <returns>字符数组。</returns>
    private static Rune[] Runes(string text)
    {
        var list = new List<Rune>(text.Length);
        foreach (var rune in text.Replace("\r\n", "\n", StringComparison.Ordinal).EnumerateRunes())
        {
            var value = rune.Value switch
            {
                '\r' => new Rune('\n'),
                '\t' => new Rune(' '),
                _ => rune,
            };
            if (value.Value == '\n' || !Rune.IsControl(value))
            {
                list.Add(value);
            }
        }

        return list.ToArray();
    }

    /// <summary>
    /// 磅（pt）换算成打印点。
    /// </summary>
    /// <param name="points">磅数。</param>
    /// <param name="dpi">分辨率。</param>
    /// <returns>打印点。</returns>
    private static float Dots(double points, int dpi) => (float)LabelUnits.PointsToDots(points, dpi);
}

/// <summary>
/// 排版结果。
/// </summary>
/// <param name="FontSize">实际字号（打印点）。</param>
/// <param name="LineBox">行高（打印点）= 字号 × lineHeight。</param>
/// <param name="Lines">各行。</param>
/// <param name="Clipped">是否有内容超出框被裁掉。</param>
internal sealed record TextBlock(float FontSize, float LineBox, IReadOnlyList<TextLine> Lines, bool Clipped);

/// <summary>
/// 一行文本。坐标是打印点，相对元素框左上角。
/// </summary>
/// <param name="Glyphs">字形。</param>
/// <param name="X">每个字形的横坐标。</param>
/// <param name="Left">行的左端。</param>
/// <param name="Width">行宽（不含行尾空格）。</param>
/// <param name="Top">行框的上沿。</param>
/// <param name="Baseline">基线。</param>
internal sealed record TextLine(ushort[] Glyphs, float[] X, float Left, float Width, float Top, float Baseline);
