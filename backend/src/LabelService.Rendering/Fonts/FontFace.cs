using SkiaSharp;

namespace LabelService.Rendering.Fonts;

/// <summary>
/// 一个可直接用来排版和绘制的字体：字形、粗细斜体（缺少对应字形时用合成粗体、合成斜体），以及缓存好的 SKFont。
/// </summary>
/// <remarks>
/// SKFont 固定用参考字号 <see cref="ReferenceSize"/> 创建，排版时把宽度按比例换算到实际字号，绘制时用画布缩放。
/// 这样每个字体只有两个 SKFont（抗锯齿、黑白），渲染时不再创建；关掉 hinting 后字宽和字号严格成正比。
/// 加载后只读，多线程共用。
/// </remarks>
internal sealed class FontFace : IDisposable
{
    /// <summary>
    /// 测量和绘制用的参考字号（Skia 单位）。
    /// </summary>
    public const float ReferenceSize = 100;

    /// <summary>
    /// 合成斜体的倾斜量，和浏览器一致。
    /// </summary>
    private const float FakeItalicSkew = -0.25f;

    private readonly SKFont smooth;
    private readonly SKFont aliased;

    public FontFace(SKTypeface typeface, bool fakeBold, bool fakeItalic)
    {
        Typeface = typeface;
        FakeBold = fakeBold;
        FakeItalic = fakeItalic;
        smooth = Create(SKFontEdging.Antialias);
        aliased = Create(SKFontEdging.Alias);
        var metrics = smooth.Metrics;
        Ascent = -metrics.Ascent / ReferenceSize;
        Descent = metrics.Descent / ReferenceSize;
    }

    /// <summary>字形。</summary>
    public SKTypeface Typeface
    {
        get;
    }

    /// <summary>字体家族名（字体文件里的名字）。</summary>
    public string FamilyName => Typeface.FamilyName;

    /// <summary>是否合成粗体。</summary>
    public bool FakeBold
    {
        get;
    }

    /// <summary>是否合成斜体。</summary>
    public bool FakeItalic
    {
        get;
    }

    /// <summary>基线以上的高度，单位：字号（1 = 一个字号）。</summary>
    public float Ascent
    {
        get;
    }

    /// <summary>基线以下的深度，单位：字号。</summary>
    public float Descent
    {
        get;
    }

    /// <summary>
    /// 绘制用的 SKFont（参考字号）。黑白输出不抗锯齿。
    /// </summary>
    public SKFont Font(bool mono) => mono ? aliased : smooth;

    /// <summary>
    /// 查字形和字宽。字宽的单位是字号（1 = 一个字号），乘以实际字号就是实际宽度。
    /// 部首字符（如 ⽇ U+2F47）按对应的汉字查字形，见 <see cref="RadicalCmap"/>。
    /// </summary>
    public void Measure(ReadOnlySpan<int> codepoints, Span<ushort> glyphs, Span<float> advances)
    {
        var lookup = codepoints;
        foreach (var codepoint in codepoints)
        {
            if (RadicalCmap.IsRadical(codepoint))
            {
                var unified = new int[codepoints.Length];
                for (var i = 0; i < unified.Length; i++)
                {
                    unified[i] = RadicalCmap.Unify(codepoints[i]);
                }

                lookup = unified;
                break;
            }
        }

        smooth.GetGlyphs(lookup, glyphs);
        smooth.GetGlyphWidths(glyphs, advances, Span<SKRect>.Empty, null);
        for (var i = 0; i < advances.Length; i++)
        {
            advances[i] /= ReferenceSize;
        }
    }

    public void Dispose()
    {
        smooth.Dispose();
        aliased.Dispose();
    }

    private SKFont Create(SKFontEdging edging) => new(Typeface, ReferenceSize)
    {
        Edging = edging,
        Hinting = SKFontHinting.None,
        Subpixel = true,
        LinearMetrics = true,
        BaselineSnap = false,
        Embolden = FakeBold,
        SkewX = FakeItalic ? FakeItalicSkew : 0,
    };
}

/// <summary>
/// 一个字体家族的全部字形，以及按“粗体 × 斜体”选好的四个 <see cref="FontFace"/>。
/// </summary>
internal sealed class FontFamily(string name) : IDisposable
{
    private readonly List<(SKTypeface Typeface, int Weight, bool Italic)> typefaces = [];
    private FontFace[]? faces;

    /// <summary>家族名。</summary>
    public string Name { get; } = name;

    /// <summary>
    /// 加入一个字形。只在加载阶段调用。
    /// </summary>
    public void Add(SKTypeface typeface, int weight, bool italic)
    {
        typefaces.Add((typeface, weight, italic));
        faces = null;
    }

    /// <summary>
    /// 加载结束后调用一次，选好四种组合，之后只读。
    /// </summary>
    public void Seal() => faces = [Choose(false, false), Choose(true, false), Choose(false, true), Choose(true, true)];

    /// <summary>
    /// 取对应粗细、斜体的字体。
    /// </summary>
    public FontFace Face(bool bold, bool italic) =>
        (faces ?? throw new InvalidOperationException("字体家族还没有加载完成"))[(bold ? 1 : 0) + (italic ? 2 : 0)];

    public void Dispose()
    {
        foreach (var face in faces ?? [])
        {
            face.Dispose();
        }
    }

    /// <summary>
    /// 先找斜体一致的字形，没有就用正体合成斜体；再取字重最接近的（常规 400、粗体 700），粗体缺字形时合成。
    /// </summary>
    private FontFace Choose(bool bold, bool italic)
    {
        var candidates = typefaces.Where(item => item.Italic == italic).ToList();
        var fakeItalic = false;
        if (candidates.Count == 0)
        {
            candidates = typefaces;
            fakeItalic = italic;
        }

        var target = bold ? 700 : 400;
        var best = candidates.MinBy(item => Math.Abs(item.Weight - target));
        return new FontFace(best.Typeface, fakeBold: bold && best.Weight < 600, fakeItalic);
    }
}
