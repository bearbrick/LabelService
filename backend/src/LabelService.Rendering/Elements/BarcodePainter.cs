using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Fonts;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 一维码：模块宽度 = floor(去掉两侧空白区后的框宽点数 ÷ 模块数)，每条、每个空都是整数个打印点（设计书第 5 节“一维码尺寸规则”）。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>模块宽度小于 minModuleDots（默认 2 点）时抛 BARCODE_TOO_DENSE，内容不符合码制时抛 BARCODE_INVALID。</item>
/// <item>条码按 align 放在框里，左端对齐到整点；条不抗锯齿，黑白分明。</item>
/// <item>人眼可读文字：字号默认 8pt，占“字号 × 1.2”的高度，和条之间隔 0.12 个字号，在条码下方（或上方）居中，和设计器一致。</item>
/// <item>旋转对齐到最近的直角（模板发布时已经拦截非直角）；旋转中心微调到半个点上，转 90° 后条边仍落在整点。</item>
/// </list>
/// </remarks>
internal sealed class BarcodePainter : IElementPainter
{
    /// <summary>
    /// 人眼可读文字的默认字号（pt），和设计器一致。
    /// </summary>
    public const double DefaultCaptionSize = 8;

    [ThreadStatic]
    private static SKPaint? threadPaint;

    public string ElementType => ElementTypes.Barcode;

    public ElementLayout Layout(LabelElement element, RenderContext context)
    {
        var barcode = (BarcodeElement)element;
        var symbol = LinearSymbols.Encode(barcode.Symbology, context.Bind(barcode.Value), element.Id);
        var box = context.Box(element);
        var quiet = context.Dots(Math.Max(0, barcode.QuietZone ?? 0));
        var usable = box.Width - quiet * 2;
        var dots = (int)Math.Floor(usable / symbol.Columns);
        var minDots = Math.Max(1, barcode.MinModuleDots);
        if (dots < minDots)
        {
            var needMm = Math.Ceiling(((double)symbol.Columns * minDots / context.Dpi * LabelUnits.MmPerInch + Math.Max(0, barcode.QuietZone ?? 0) * 2) * 10) / 10;
            throw new LabelRenderException(
                ErrorCodes.BarcodeTooDense,
                $"元素 {element.Id} 的条码过密：{symbol.Columns} 个模块，框宽只够每个模块 {Math.Max(0, dots)} 点（至少 {minDots} 点 @{context.Dpi}dpi）。把宽度加到 {needMm} mm，或缩短内容",
                element.Id);
        }

        var style = barcode.TextStyle ?? new TextStyle();
        var captionPoints = style.FontSize is > 0 ? style.FontSize.Value : DefaultCaptionSize;
        var captionSize = (float)LabelUnits.PointsToDots(captionPoints, context.Dpi);
        var captionHeight = barcode.ShowText ? captionSize * 1.2f : 0;
        var top = barcode.ShowText && barcode.TextPosition == BarcodeTextPosition.Top;
        var barsWidth = symbol.Columns * dots;
        var offset = barcode.Align switch
        {
            TextAlign.Center => (usable - barsWidth) / 2,
            TextAlign.Right => usable - barsWidth,
            _ => 0,
        };
        var left = (int)Math.Round(box.Left + quiet + offset);
        var barTop = (int)Math.Round(box.Top + (top ? captionHeight : 0));
        var barBottom = (int)Math.Round(top ? box.Bottom : box.Bottom - captionHeight);
        barBottom = Math.Max(barBottom, barTop + 1);
        var bars = SKRectI.Create(left, barTop, barsWidth, barBottom - barTop);

        Caption? caption = null;
        if (barcode.ShowText && symbol.Caption.Length > 0)
        {
            caption = LayoutCaption(element.Id, symbol.Caption, style, captionPoints, box, bars, top, context);
        }

        return new BarcodeLayout(element, box, symbol, dots, bars, caption);
    }

    public void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context)
    {
        var barcode = (BarcodeLayout)layout;
        var paint = threadPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false, Color = SKColors.Black };
        var symbol = barcode.Symbol;
        var x = barcode.Bars.Left;
        for (var i = 0; i < symbol.Columns;)
        {
            var dark = symbol.Modules[i];
            var run = 1;
            while (i + run < symbol.Columns && symbol.Modules[i + run] == dark)
            {
                run++;
            }

            if (dark)
            {
                canvas.DrawRect(SKRect.Create(x + i * barcode.ModuleDots, barcode.Bars.Top, run * barcode.ModuleDots, barcode.Bars.Height), paint);
            }

            i += run;
        }

        if (barcode.Caption is { } caption)
        {
            canvas.Save();
            canvas.ClipRect(layout.Bounds);
            TextPainter.DrawBlock(canvas, caption.Origin, caption.Face, caption.Block, caption.Color, underline: false, context.Mono);
            canvas.Restore();
        }
    }

    /// <summary>
    /// 人眼可读文字：在条码正下方（或正上方）居中；比条码宽时以条码为中心向两边展开，最宽到元素框宽，超出框的部分裁掉。
    /// </summary>
    private static Caption LayoutCaption(string elementId, string text, TextStyle style, double points, SKRect box, SKRectI bars, bool top, RenderContext context)
    {
        var size = (float)LabelUnits.PointsToDots(points, context.Dpi);
        var face = context.Fonts.Resolve(style.FontFamily, style.Bold == true, style.Italic == true, context.Warnings);
        var captionStyle = new TextStyle
        {
            FontSize = points,
            LetterSpacing = style.LetterSpacing,
            Align = TextAlign.Center,
            VAlign = VerticalAlign.Top,
            Fit = TextFit.None,
            LineHeight = 1,
        };
        var natural = TextLayout.Arrange(text, captionStyle, face, float.MaxValue / 4, size, context.Dpi).Lines.Max(line => line.Width);
        var width = Math.Max(bars.Width, Math.Min(box.Width, natural));
        var x = bars.Width >= width ? bars.Left : bars.Left + (bars.Width - width) / 2;
        var y = top ? box.Top : bars.Bottom + size * 0.12f;
        var block = TextLayout.Arrange(text, captionStyle, face, width, size, context.Dpi);
        if (natural > box.Width + 0.5f)
        {
            context.Warnings.Add(WarningCodes.TextClipped + ":" + Uri.EscapeDataString(elementId));
        }

        return new Caption(new SKPoint(x, y), face, block, RenderContext.Color(style.Color, SKColors.Black));
    }

    private sealed record Caption(SKPoint Origin, FontFace Face, TextBlock Block, SKColor Color);

    /// <summary>
    /// 一维码的排版结果。
    /// </summary>
    private sealed class BarcodeLayout(LabelElement element, SKRect bounds, EncodedSymbol symbol, int moduleDots, SKRectI bars, Caption? caption)
        : ElementLayout(element, bounds)
    {
        public EncodedSymbol Symbol { get; } = symbol;

        public int ModuleDots { get; } = moduleDots;

        public SKRectI Bars { get; } = bars;

        public Caption? Caption { get; } = caption;

        /// <summary>
        /// 对齐到最近的直角。
        /// </summary>
        public override double Rotation => ((Math.Round(Element.Rotation / 90) * 90 % 360) + 360) % 360;

        public override SKPoint Pivot => SymbolGeometry.Pivot(Bounds, Rotation);
    }
}
