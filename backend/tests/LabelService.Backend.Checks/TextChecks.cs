using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Elements;
using LabelService.Rendering.Fonts;
using LabelService.Rendering.Validation;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// 文本：字体匹配和降级、换行、自动缩小、对齐、行高、字间距，以及渲染出来的像素。
/// 字体来自 backend/assets/fonts（准备方法见那里的 README）。
/// </summary>
internal static class TextChecks
{
    /// <summary>
    /// 检查用的分辨率。
    /// </summary>
    private const int Dpi = 300;

    /// <summary>
    /// 运行文本的全部检查；没有思源黑体时只报这一项。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    public static void Run(string backendRoot, Action<string, bool> check)
    {
        var fonts = Fixtures.Fonts(backendRoot);
        check("backend/assets/fonts 里有思源黑体（没有时见那里的 README）", fonts.HasDefaultFamily);
        if (!fonts.HasDefaultFamily)
        {
            return;
        }

        CheckFonts(fonts, check);
        CheckLayout(fonts, check);
        CheckPixels(backendRoot, fonts, check);
    }

    /// <summary>
    /// 字体匹配：模板字体名认别名，粗体、斜体选对字形或合成，找不到时降级并给 FONT_FALLBACK。
    /// </summary>
    /// <param name="fonts">字体注册表。</param>
    /// <param name="check">记录检查结果。</param>
    private static void CheckFonts(FontRegistry fonts, Action<string, bool> check)
    {
        var warnings = new List<string>();
        var regular = fonts.Resolve("SourceHanSansSC", bold: false, italic: false, warnings);
        check("SourceHanSansSC 匹配到思源黑体（或同款的 Noto Sans SC），没有警告",
            warnings.Count == 0 && regular.FamilyName.Replace(" ", "") is "SourceHanSansSC" or "SourceHanSansCN" or "NotoSansSC" or "NotoSansCJKSC");
        check("同一字体反复取到同一个实例（字体和 SKFont 不在每次请求时创建）",
            ReferenceEquals(regular, fonts.Resolve("Source Han Sans SC", false, false, warnings)) && ReferenceEquals(regular, fonts.Resolve(null, false, false, warnings)));
        var bold = fonts.Resolve("SourceHanSansSC", bold: true, italic: false, warnings);
        check("粗体用 700 字重的字形，没有时合成粗体", bold.Typeface.FontWeight >= 600 || bold.FakeBold);
        check("思源黑体常规字重是 400 左右（可变字体不用默认的最细字重）", regular.Typeface.FontWeight is >= 350 and <= 450);
        check("没有斜体字形时合成斜体", fonts.Resolve("SourceHanSansSC", false, italic: true, warnings).FakeItalic || fonts.Resolve("SourceHanSansSC", false, true, warnings).Typeface.IsItalic);
        check("中文字形齐全（示例模板里的字）", regular.Typeface.GetGlyphs("XX科技有限公司成品箱标品名数量批次日期：").All(glyph => glyph != 0));

        var fallback = fonts.Resolve("微软雅黑", false, false, warnings);
        check("不存在的字体降级到思源黑体，并给 FONT_FALLBACK:原字体名",
            ReferenceEquals(fallback, regular) && warnings.SequenceEqual([WarningCodes.FontFallback + ":" + Uri.EscapeDataString("微软雅黑")]));
        var serifWarnings = new List<string>();
        var serif = fonts.Resolve("SourceHanSerifSC", false, false, serifWarnings);
        check("SourceHanSerifSC 匹配到思源宋体；没有宋体文件时降级并给警告",
            serif.FamilyName.Contains("Serif", StringComparison.Ordinal) ? serifWarnings.Count == 0 : serifWarnings.Count == 1);
    }

    /// <summary>
    /// 排版规则：不换行时裁切、自动换行（中文按字、英文按单词、避头尾）、自动缩小、对齐、行高、字间距。
    /// </summary>
    /// <param name="fonts">字体注册表。</param>
    /// <param name="check">记录检查结果。</param>
    private static void CheckLayout(FontRegistry fonts, Action<string, bool> check)
    {
        var face = fonts.Resolve(null, false, false, new List<string>());
        var em = (float)LabelUnits.PointsToDots(10, Dpi);
        TextBlock Arrange(string text, TextStyle style, float widthEm, float heightEm = 10) =>
            TextLayout.Arrange(text, style, face, widthEm * em, heightEm * em, Dpi);

        // 汉字等宽 1em：框宽 4.5em 每行放 4 个字。
        var chinese = Arrange("一二三四五六七八九十", new TextStyle { Fit = TextFit.Wrap }, 4.5f);
        check("wrap：中文按字换行", chinese.Lines.Select(line => line.Glyphs.Length).SequenceEqual([4, 4, 2]));
        var english = Arrange("hello world foo", new TextStyle { Fit = TextFit.Wrap }, WidthEm(face, "hello world") + 0.3f);
        check("wrap：英文按单词换行，行尾空格不计宽度", english.Lines.Select(line => line.Glyphs.Length).SequenceEqual([11, 3]));
        var longWord = Arrange("abcdefghijklmnopqrstuvwxyz", new TextStyle { Fit = TextFit.Wrap }, 4);
        check("wrap：单词比框还宽时按字符断开，每行不超框", longWord.Lines.Count > 1 && longWord.Lines.All(line => line.Width <= 4 * em + 0.01f));
        var punctuation = Arrange("一二三，四", new TextStyle { Fit = TextFit.Wrap }, 3.2f);
        check("wrap：逗号不放行首，连同前一个字一起换到下一行", punctuation.Lines.Select(line => line.Glyphs.Length).SequenceEqual([2, 3]));
        var tall = Arrange("一二三四五六七八九十", new TextStyle { Fit = TextFit.Wrap, LineHeight = 1.2 }, 4.5f, heightEm: 2);
        check("wrap：超出框高的部分裁掉并标记", tall is { Lines.Count: 3, Clipped: true });

        var none = Arrange("一二三四五六七八九十", new TextStyle(), 5);
        check("none：单行，超出框的部分裁掉并标记", none is { Lines.Count: 1, Clipped: true } && none.Lines[0].Glyphs.Length == 10);
        var shrink = Arrange("一二三四五六七八九十", new TextStyle { Fit = TextFit.Shrink }, 5);
        check("shrink：缩小到放得下，宽度不超框，不标记裁切",
            shrink is { Lines.Count: 1, Clipped: false } && shrink.FontSize < em && shrink.Lines[0].Width <= 5 * em + 0.05f && shrink.Lines[0].Width > 4.8f * em);
        var floor = Arrange("一二三四五六七八九十", new TextStyle { Fit = TextFit.Shrink, MinFontSize = 6 }, 2);
        check("shrink：缩到 minFontSize 仍放不下就裁掉并标记",
            floor.Clipped && Math.Abs(floor.FontSize - (float)LabelUnits.PointsToDots(6, Dpi)) < 0.01f);
        var fits = Arrange("一二", new TextStyle { Fit = TextFit.Shrink }, 5);
        check("shrink：放得下时不缩小", Math.Abs(fits.FontSize - em) < 0.001f);

        var right = Arrange("一二", new TextStyle { Align = TextAlign.Right, VAlign = VerticalAlign.Top }, 5, 3);
        var center = Arrange("一二", new TextStyle { Align = TextAlign.Center, VAlign = VerticalAlign.Bottom }, 5, 3);
        var middle = Arrange("一二", new TextStyle(), 5, 3);
        check("对齐：右对齐贴右边，居中两边留白相等",
            Math.Abs(right.Lines[0].Left + right.Lines[0].Width - 5 * em) < 0.01f && Math.Abs(center.Lines[0].Left - (5 * em - center.Lines[0].Width) / 2) < 0.01f);
        check("垂直对齐：上对齐从框顶开始，下对齐贴框底，默认垂直居中",
            right.Lines[0].Top == 0 && Math.Abs(center.Lines[0].Top + center.LineBox - 3 * em) < 0.01f
            && Math.Abs(middle.Lines[0].Top - (3 * em - middle.LineBox) / 2) < 0.01f);
        var spaced = Arrange("a\nb", new TextStyle { LineHeight = 2 }, 5);
        check("内容里的换行符换行，行高 = 字号 × lineHeight", spaced.Lines.Count == 2 && Math.Abs(spaced.Lines[1].Top - spaced.Lines[0].Top - 2 * em) < 0.01f);
        var plain = Arrange("一二三四五", new TextStyle(), 20);
        var letter = Arrange("一二三四五", new TextStyle { LetterSpacing = 1 }, 20);
        check("字间距加在相邻两字之间（5 个字加 4 个间距）",
            Math.Abs(letter.Lines[0].Width - plain.Lines[0].Width - 4 * (float)LabelUnits.MmToDots(1, Dpi)) < 0.01f);
        check("基线在行内垂直居中（ascent、descent 居中）",
            Math.Abs(middle.Lines[0].Baseline - (middle.Lines[0].Top + (middle.LineBox - (face.Ascent + face.Descent) * em) / 2 + face.Ascent * em)) < 0.01f);
    }

    /// <summary>
    /// 渲染出来的像素：示例箱标的文字落在框里，裁掉的部分不画，下划线、颜色正确。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="fonts">字体注册表。</param>
    /// <param name="check">记录检查结果。</param>
    private static void CheckPixels(string backendRoot, FontRegistry fonts, Action<string, bool> check)
    {
        var renderer = new LabelRenderer(fonts, Fixtures.Assets(backendRoot));
        var template = Fixtures.LoadTemplate(backendRoot);
        var values = FieldValidator.Validate(template.Fields, null, Fixtures.Data(Fixtures.BoxLabelData)).Values;
        var sample = Render(renderer, template, values);
        using (var bitmap = SKBitmap.Decode(sample.Content.ToArray()))
        {
            var empty = template.Elements.OfType<TextElement>().Where(element => DarkPixels(bitmap, element.X, element.Y, element.Width, element.Height) < 30).Select(element => element.Id).ToArray();
            check("示例模板的 6 个文本元素都画出来了" + (empty.Length > 0 ? "，空白的：" + string.Join(", ", empty) : ""), empty.Length == 0);
        }

        check("示例模板没有字体降级和裁切警告", !sample.Warnings.Any(warning => warning.StartsWith(WarningCodes.FontFallback) || warning.StartsWith(WarningCodes.TextClipped)));

        var overflow = Single(new TextElement { Id = "long", X = 10, Y = 10, Width = 20, Height = 6, Content = "标签服务测试标签服务测试标签服务" });
        var overflowResult = Render(renderer, overflow, values);
        using (var bitmap = SKBitmap.Decode(overflowResult.Content.ToArray()))
        {
            check("文本落在框内：超出的部分裁掉，框外没有墨迹",
                DarkPixels(bitmap, 10, 10, 20, 6) > 100 && DarkPixels(bitmap, 30.2, 8, 15, 10) == 0 && DarkPixels(bitmap, 0, 0, 9.8, 30) == 0);
        }

        check("超出框时给 TEXT_CLIPPED:元素 ID", overflowResult.Warnings.Contains(WarningCodes.TextClipped + ":long"));

        var shrink = Single(new TextElement
        {
            Id = "shrink",
            X = 10,
            Y = 10,
            Width = 20,
            Height = 6,
            Content = "标签服务测试标签服务测试标签服务",
            Style = new TextStyle { Fit = TextFit.Shrink, MinFontSize = 2, Align = TextAlign.Center },
        });
        var shrinkResult = Render(renderer, shrink, values);
        using (var bitmap = SKBitmap.Decode(shrinkResult.Content.ToArray()))
        {
            check("shrink 后宽度不超框：左右两端都有字，框外没有墨迹，没有裁切警告",
                DarkPixels(bitmap, 10, 10, 1.5, 6) > 0 && DarkPixels(bitmap, 28.5, 10, 1.5, 6) > 0
                && DarkPixels(bitmap, 30.1, 8, 15, 10) == 0 && !shrinkResult.Warnings.Any(warning => warning.StartsWith(WarningCodes.TextClipped)));
        }

        var rotated = Single(new TextElement
        {
            Id = "rotated",
            X = 5,
            Y = 17,
            Width = 30,
            Height = 6,
            Rotation = 90,
            Content = "标签服务测试标签服务",
            Style = new TextStyle { Fit = TextFit.Shrink, Align = TextAlign.Center },
        });
        using (var bitmap = SKBitmap.Decode(Render(renderer, rotated, values).Content.ToArray()))
        {
            // 框中心 (20, 20)，转 90° 后变成 x 17–23、y 5–35 的竖条。
            check("旋转 90° 的文本变成竖排的一条：竖条里有字，原来横向的位置没有",
                DarkPixels(bitmap, 17, 6, 6, 4) > 0 && DarkPixels(bitmap, 17, 30, 6, 4) > 0 && DarkPixels(bitmap, 6, 17, 10, 6) == 0);
        }

        var underline = Single(new TextElement
        {
            Id = "underline",
            X = 5,
            Y = 10,
            Width = 30,
            Height = 8,
            Content = "ii      ii",
            Style = new TextStyle { FontSize = 14, Underline = true },
        });
        using (var bitmap = SKBitmap.Decode(Render(renderer, underline, values).Content.ToArray()))
        {
            // 两组字之间是空格，那里只有下划线：深色像素集中在几行里，横向连续。
            var rows = DarkRows(bitmap, 9, 10, 3, 8);
            check("下划线连续：两组字之间的空白处也有一条横线", rows is > 0 and < 10 && DarkPixels(bitmap, 9, 10, 3, 8) >= rows * Dot(3) * 0.9);
        }

        var unknown = Single(new TextElement { Id = "font", X = 5, Y = 5, Width = 30, Height = 6, Content = "字体", Style = new TextStyle { FontFamily = "SimSun" } });
        check("渲染结果里带 FONT_FALLBACK:SimSun", Render(renderer, unknown, values).Warnings.Contains(WarningCodes.FontFallback + ":SimSun"));
    }

    /// <summary>
    /// 一段文字的宽度，单位是字号（不含字间距）。
    /// </summary>
    /// <param name="face">字体。</param>
    /// <param name="text">文字。</param>
    /// <returns>宽度，乘以字号就是实际宽度。</returns>
    private static float WidthEm(FontFace face, string text)
    {
        var codepoints = text.EnumerateRunes().Select(rune => rune.Value).ToArray();
        var advances = new float[codepoints.Length];
        face.Measure(codepoints, new ushort[codepoints.Length], advances);
        return advances.Sum();
    }

    /// <summary>
    /// 只有一个文本元素的 40×40 模板。
    /// </summary>
    /// <param name="element">文本元素。</param>
    /// <returns>模板。</returns>
    private static LabelTemplate Single(TextElement element) => new()
    {
        Code = "TEXT",
        Page = new LabelPage { Width = 40, Height = 40 },
        Elements = [element],
    };

    /// <summary>
    /// 渲染一张 PNG。
    /// </summary>
    /// <param name="renderer">渲染引擎。</param>
    /// <param name="template">模板。</param>
    /// <param name="values">字段值。</param>
    /// <returns>渲染结果。</returns>
    private static RenderResult Render(LabelRenderer renderer, LabelTemplate template, IReadOnlyDictionary<string, object?> values) =>
        renderer.Render(new RenderJob { Template = template, Labels = [new LabelInstance(values)], Format = LabelFormat.Png, Dpi = Dpi });

    /// <summary>
    /// 毫米换算成整数打印点。
    /// </summary>
    /// <param name="mm">毫米。</param>
    /// <returns>打印点。</returns>
    private static int Dot(double mm) => (int)Math.Round(LabelUnits.MmToDots(mm, Dpi));

    /// <summary>
    /// 区域内有深色像素的行数。
    /// </summary>
    /// <param name="bitmap">位图。</param>
    /// <param name="xMm">区域左边，mm。</param>
    /// <param name="yMm">区域上边，mm。</param>
    /// <param name="widthMm">区域宽度，mm。</param>
    /// <param name="heightMm">区域高度，mm。</param>
    /// <returns>行数。</returns>
    private static int DarkRows(SKBitmap bitmap, double xMm, double yMm, double widthMm, double heightMm) =>
        Enumerable.Range(Dot(yMm), Dot(heightMm))
            .Count(y => Enumerable.Range(Dot(xMm), Dot(widthMm)).Any(x => bitmap.GetPixel(x, y).Red < 128));

    /// <summary>
    /// 区域内的深色像素数。区域用 mm，超出图片的部分忽略。
    /// </summary>
    /// <param name="bitmap">位图。</param>
    /// <param name="xMm">区域左边，mm。</param>
    /// <param name="yMm">区域上边，mm。</param>
    /// <param name="widthMm">区域宽度，mm。</param>
    /// <param name="heightMm">区域高度，mm。</param>
    /// <returns>深色像素的个数。</returns>
    private static int DarkPixels(SKBitmap bitmap, double xMm, double yMm, double widthMm, double heightMm)
    {
        var count = 0;
        var x0 = Math.Max(0, Dot(xMm));
        var y0 = Math.Max(0, Dot(yMm));
        var x1 = Math.Min(bitmap.Width, Math.Max(x0 + 1, Dot(xMm + widthMm)));
        var y1 = Math.Min(bitmap.Height, Math.Max(y0 + 1, Dot(yMm + heightMm)));
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                if (bitmap.GetPixel(x, y).Red < 128)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
