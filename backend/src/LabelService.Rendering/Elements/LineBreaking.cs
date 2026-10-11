using System.Text;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 文本分行：按换行符分段；自动换行时按框宽贪心断行，中文按字、英文按单词，遵守避头尾规则（和设计器一致）。
/// </summary>
internal static class LineBreaking
{
    /// <summary>
    /// 不能放在行首的字符：结束标点、百分号、省略号、破折号等。
    /// </summary>
    private static readonly HashSet<int> NoLineStart =
        "，。、；：？！）】》」』〉〕｝,.;:?!)]}%…—～·”’".Select(ch => (int)ch).ToHashSet();

    /// <summary>
    /// 不能放在行尾的字符：开始标点。
    /// </summary>
    private static readonly HashSet<int> NoLineEnd = "（【《「『〈〔｛([{“‘".Select(ch => (int)ch).ToHashSet();

    /// <summary>
    /// 不自动换行：按换行符分行。
    /// </summary>
    /// <param name="text">测好字宽的文本。</param>
    /// <returns>每行在文本里的位置（已去掉行尾空格）。</returns>
    public static List<TextSpan> Paragraphs(MeasuredText text)
    {
        var lines = new List<TextSpan>();
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
    /// <param name="text">测好字宽的文本。</param>
    /// <param name="size">字号（打印点）。</param>
    /// <param name="width">框宽（打印点）。</param>
    /// <returns>每行在文本里的位置（已去掉行尾空格）。</returns>
    public static List<TextSpan> Wrap(MeasuredText text, float size, float width)
    {
        var lines = new List<TextSpan>();
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
                // 折行后新行开头的空格不要（段落开头的空格保留，那是用户有意缩进）。
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

                // 一个字一个字往行里放，记下最后一个能断开的位置；放不下（空格除外，行尾空格不占宽度）就停。
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

                // 段落放完了就到段尾；否则在最后一个能断的位置断，一个都没有（单词比框还宽）就在放不下的地方硬断。
                var stop = j >= end ? end : lastBreak > start ? lastBreak : j;
                lines.Add(text.TrimEnd(start, stop));
                i = stop;
            }
        }

        return lines;
    }

    /// <summary>
    /// 是否空格（半角或全角）。
    /// </summary>
    /// <param name="rune">字符。</param>
    /// <returns>是空格时为 true。</returns>
    public static bool IsSpace(Rune rune) => rune.Value is ' ' or '　';

    /// <summary>
    /// 段落的结尾：从 <paramref name="start"/> 往后第一个换行符的位置，没有换行符时是文本末尾。
    /// </summary>
    /// <param name="text">测好字宽的文本。</param>
    /// <param name="start">段落开头。</param>
    /// <returns>段落结尾（不含）。</returns>
    private static int ParagraphEnd(MeasuredText text, int start)
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
    /// <param name="runes">全部字符。</param>
    /// <param name="index">要断开的位置（这个字符会成为新行的第一个字），大于 0。</param>
    /// <returns>可以断开时为 true。</returns>
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

    /// <summary>
    /// 是否中日韩文字（汉字、假名、韩文、全角字符、中文标点）：这类文字之间可以随处断行。
    /// </summary>
    /// <param name="rune">字符。</param>
    /// <returns>是中日韩文字时为 true。</returns>
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
}

/// <summary>
/// 一段文本按参考字号测好的字宽。
/// </summary>
/// <param name="runes">文本的字符。</param>
/// <param name="advances">每个字符的前进宽度，单位是字号（乘以字号得到打印点）。</param>
/// <param name="spacing">字间距（打印点），加在相邻两个字之间。</param>
internal sealed class MeasuredText(Rune[] runes, float[] advances, float spacing)
{
    /// <summary>
    /// 文本的字符。
    /// </summary>
    public Rune[] Runes { get; } = runes;

    /// <summary>
    /// 每个字符的前进宽度，单位是字号。
    /// </summary>
    public float[] Advances { get; } = advances;

    /// <summary>
    /// 字间距（打印点）。
    /// </summary>
    public float Spacing { get; } = spacing;

    /// <summary>
    /// 一行在某个字号下的宽度：字宽之和乘以字号，加上字之间的字间距。
    /// </summary>
    /// <param name="span">行在文本里的位置。</param>
    /// <param name="size">字号（打印点）。</param>
    /// <returns>行宽（打印点）；空行为 0。</returns>
    public float Width(TextSpan span, float size)
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

    /// <summary>
    /// 去掉行尾空格，空格不参与宽度和对齐。
    /// </summary>
    /// <param name="start">行开头。</param>
    /// <param name="end">行结尾（不含）。</param>
    /// <returns>去掉行尾空格后的位置。</returns>
    public TextSpan TrimEnd(int start, int end)
    {
        while (end > start && LineBreaking.IsSpace(Runes[end - 1]))
        {
            end--;
        }

        return new TextSpan(start, end - start);
    }
}

/// <summary>
/// 一行在文本里的位置。
/// </summary>
/// <param name="Start">第一个字符的下标。</param>
/// <param name="Length">字符个数。</param>
internal readonly record struct TextSpan(int Start, int Length);
