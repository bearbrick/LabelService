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

    /// <summary>
    /// 抗锯齿的 SKFont（彩色、灰度输出用）。
    /// </summary>
    private readonly SKFont smooth;

    /// <summary>
    /// 不抗锯齿的 SKFont（黑白输出用，边缘不出现灰点）。
    /// </summary>
    private readonly SKFont aliased;

    /// <summary>
    /// 用字形创建，读出字体的上下高度。
    /// </summary>
    /// <param name="typeface">字形。</param>
    /// <param name="fakeBold">字体家族里没有粗体字形时为 true，用合成粗体。</param>
    /// <param name="fakeItalic">字体家族里没有斜体字形时为 true，用合成斜体。</param>
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

    /// <summary>
    /// 字形。
    /// </summary>
    public SKTypeface Typeface
    {
        get;
    }

    /// <summary>
    /// 字体家族名（字体文件里的名字）。
    /// </summary>
    public string FamilyName => Typeface.FamilyName;

    /// <summary>
    /// 是否合成粗体。
    /// </summary>
    public bool FakeBold
    {
        get;
    }

    /// <summary>
    /// 是否合成斜体。
    /// </summary>
    public bool FakeItalic
    {
        get;
    }

    /// <summary>
    /// 基线以上的高度，单位：字号（1 = 一个字号）。
    /// </summary>
    public float Ascent
    {
        get;
    }

    /// <summary>
    /// 基线以下的深度，单位：字号。
    /// </summary>
    public float Descent
    {
        get;
    }

    /// <summary>
    /// 绘制用的 SKFont（参考字号）。黑白输出不抗锯齿。
    /// </summary>
    /// <param name="mono">是否黑白输出。</param>
    /// <returns>缓存好的 SKFont，不要释放。</returns>
    public SKFont Font(bool mono) => mono ? aliased : smooth;

    /// <summary>
    /// 查字形和字宽。字宽的单位是字号（1 = 一个字号），乘以实际字号就是实际宽度。
    /// 部首字符（如 ⽇ U+2F47）按对应的汉字查字形，见 <see cref="RadicalCmap"/>。
    /// </summary>
    /// <param name="codepoints">要测量的码位。</param>
    /// <param name="glyphs">输出：每个码位的字形编号；字体里没有的字为 0。</param>
    /// <param name="advances">输出：每个字的前进宽度，单位是字号。</param>
    public void Measure(ReadOnlySpan<int> codepoints, Span<ushort> glyphs, Span<float> advances)
    {
        // 绝大多数文本没有部首字符，不用复制；有的话整段换成对应汉字再查（字体里的部首码位已经拿掉了）。
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

    /// <inheritdoc />
    public void Dispose()
    {
        smooth.Dispose();
        aliased.Dispose();
    }

    /// <summary>
    /// 创建参考字号的 SKFont：不做 hinting、线性度量，字宽和字号严格成正比。
    /// </summary>
    /// <param name="edging">边缘处理：抗锯齿或不抗锯齿。</param>
    /// <returns>新的 SKFont。</returns>
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
/// <param name="name">家族名。</param>
internal sealed class FontFamily(string name) : IDisposable
{
    /// <summary>
    /// 家族里的全部字形和它们的字重、是否斜体。
    /// </summary>
    private readonly List<(SKTypeface Typeface, int Weight, bool Italic)> typefaces = [];

    /// <summary>
    /// 选好的四种组合：常规、粗体、斜体、粗斜体；加载完成（<see cref="Seal"/>）前为 null。
    /// </summary>
    private FontFace[]? faces;

    /// <summary>
    /// 家族名。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 加入一个字形。只在加载阶段调用。
    /// </summary>
    /// <param name="typeface">字形。</param>
    /// <param name="weight">字重，常规 400、粗体 700。</param>
    /// <param name="italic">是否斜体字形。</param>
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
    /// <param name="bold">是否粗体。</param>
    /// <param name="italic">是否斜体。</param>
    /// <returns>选好的字体。</returns>
    /// <exception cref="InvalidOperationException">还没有调用 <see cref="Seal"/>（程序缺陷）。</exception>
    public FontFace Face(bool bold, bool italic) =>
        (faces ?? throw new InvalidOperationException("字体家族还没有加载完成"))[(bold ? 1 : 0) + (italic ? 2 : 0)];

    /// <inheritdoc />
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
    /// <param name="bold">是否粗体。</param>
    /// <param name="italic">是否斜体。</param>
    /// <returns>选好的字体。</returns>
    private FontFace Choose(bool bold, bool italic)
    {
        var candidates = typefaces.Where(item => item.Italic == italic).ToList();
        var fakeItalic = false;
        if (candidates.Count == 0)
        {
            candidates = typefaces;
            fakeItalic = italic;
        }

        // 最接近的字重不到 600（半粗）就算没有粗体字形，用合成粗体补。
        var target = bold ? 700 : 400;
        var best = candidates.MinBy(item => Math.Abs(item.Weight - target));
        return new FontFace(best.Typeface, fakeBold: bold && best.Weight < 600, fakeItalic);
    }
}
