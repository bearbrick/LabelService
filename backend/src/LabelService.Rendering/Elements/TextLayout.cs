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
/// 先按参考字号测一次字宽，换算到不同字号只做乘法，所以 shrink 的二分查找不需要重复测量。
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
    public static TextBlock Arrange(string text, TextStyle? style, FontFace face, float width, float height, int dpi)
    {
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
        var text0 = new Measured(runes, advances, spacing);

        List<Span> LinesAt(float size) => fit == TextFit.Wrap ? Wrap(text0, size, width) : Paragraphs(text0);
        bool Fits(float size)
        {
            var lines = LinesAt(size);
            return lines.Max(line => text0.Width(line, size)) <= width + fitTolerance
                && lines.Count * size * lineHeight <= height + fitTolerance;
        }

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
            var span = spans[index];
            var lineWidth = text0.Width(span, size);
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

        var clipped = widest > width + clipTolerance || total > height + clipTolerance;
        return new TextBlock(size, lineBox, lines, clipped);
    }

    /// <summary>
    /// 把文本切成 Unicode 字符（代理对算一个字）。回车统一成换行，制表符当空格，其他控制字符去掉。
    /// </summary>
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
    /// 不自动换行：按换行符分行。
    /// </summary>
    private static List<Span> Paragraphs(Measured text)
    {
        var lines = new List<Span>();
        var start = 0;
        for (var i = 0; i <= text.Runes.Length; i++)
        {
            if (i == text.Runes.Length || text.Runes[i].Value == '\n')
            {
                lines.Add(text.TrimEnd(start, i));
                start = i + 1;
            }
        }

        return lines;
    }

    /// <summary>
    /// 自动换行：每段按框宽贪心断行，在最后一个允许断开的位置断开；没有可断的位置时按字符断开。
    /// </summary>
    private static List<Span> Wrap(Measured text, float size, float width)
    {
        var lines = new List<Span>();
        foreach (var paragraph in Paragraphs(text))
        {
            var end = ParagraphEnd(text, paragraph.Start);
            if (paragraph.Length == 0)
            {
                lines.Add(paragraph);
                continue;
            }

            var i = paragraph.Start;
            var first = true;
            while (i < end)
            {
                if (!first)
                {
                    while (i < end && IsSpace(text.Runes[i]))
                    {
                        i++;
                    }

                    if (i >= end)
                    {
                        break;
                    }
                }

                first = false;
                var start = i;
                var used = 0f;
                var count = 0;
                var lastBreak = -1;
                var j = i;
                while (j < end)
                {
                    var add = (count > 0 ? text.Spacing : 0) + text.Advances[j] * size;
                    if (count > 0 && used + add > width && !IsSpace(text.Runes[j]))
                    {
                        break;
                    }

                    used += add;
                    count++;
                    j++;
                    if (j < end && CanBreakBefore(text.Runes, j))
                    {
                        lastBreak = j;
                    }
                }

                var stop = j >= end ? end : lastBreak > start ? lastBreak : j;
                lines.Add(text.TrimEnd(start, stop));
                i = stop;
            }
        }

        return lines;
    }

    private static int ParagraphEnd(Measured text, int start)
    {
        var end = start;
        while (end < text.Runes.Length && text.Runes[end].Value != '\n')
        {
            end++;
        }

        return end;
    }

    /// <summary>
    /// 能不能在 <paramref name="index"/> 之前断行：空格之后、连字符之后、中日韩文字前后可以断；
    /// 结束标点不放行首，开始标点不放行尾。
    /// </summary>
    private static bool CanBreakBefore(Rune[] runes, int index)
    {
        var previous = runes[index - 1];
        var current = runes[index];
        if (IsSpace(current))
        {
            return false;
        }

        if (IsSpace(previous))
        {
            return true;
        }

        if (NoLineStart.Contains(current.Value) || NoLineEnd.Contains(previous.Value))
        {
            return false;
        }

        return previous.Value == '-' || IsCjk(previous) || IsCjk(current);
    }

    private static bool IsSpace(Rune rune) => rune.Value is ' ' or '　';

    private static bool IsCjk(Rune rune) => rune.Value switch
    {
        >= 0x2E80 and <= 0x9FFF => true,     // 部首、标点、假名、汉字
        >= 0xAC00 and <= 0xD7AF => true,     // 韩文
        >= 0xF900 and <= 0xFAFF => true,     // 兼容汉字
        >= 0xFE30 and <= 0xFE4F => true,     // 竖排标点
        >= 0xFF00 and <= 0xFFEF => true,     // 全角字符
        >= 0x20000 and <= 0x3FFFF => true,   // 扩展汉字
        _ => false,
    };

    private static readonly HashSet<int> NoLineStart =
        "，。、；：？！）】》」』〉〕｝,.;:?!)]}%…—～·”’".Select(ch => (int)ch).ToHashSet();

    private static readonly HashSet<int> NoLineEnd = "（【《「『〈〔｛([{“‘".Select(ch => (int)ch).ToHashSet();

    private static float Dots(double points, int dpi) => (float)LabelUnits.PointsToDots(points, dpi);

    /// <summary>
    /// 一段文本按参考字号测好的字宽。
    /// </summary>
    private sealed class Measured(Rune[] runes, float[] advances, float spacing)
    {
        public Rune[] Runes { get; } = runes;

        public float[] Advances { get; } = advances;

        public float Spacing { get; } = spacing;

        public float Width(Span span, float size)
        {
            if (span.Length == 0)
            {
                return 0;
            }

            var sum = 0f;
            for (var i = span.Start; i < span.Start + span.Length; i++)
            {
                sum += Advances[i];
            }

            return sum * size + Spacing * (span.Length - 1);
        }

        /// <summary>去掉行尾空格，空格不参与宽度和对齐。</summary>
        public Span TrimEnd(int start, int end)
        {
            while (end > start && IsSpace(Runes[end - 1]))
            {
                end--;
            }

            return new Span(start, end - start);
        }
    }

    private readonly record struct Span(int Start, int Length);
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
