using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 二维码（QR、DataMatrix、PDF417）：模块大小 = floor(min(去掉空白区后的框宽, 框高)点数 ÷ 模块数)，在框里居中，模块对齐打印点。
/// </summary>
/// <remarks>
/// 模块小于 2 点时抛 BARCODE_TOO_DENSE，内容无法编码时抛 BARCODE_INVALID。模块不抗锯齿；转直角时旋转中心对齐到半个点，模块边仍在整点。
/// </remarks>
internal sealed class QrCodePainter : IElementPainter
{
    /// <summary>
    /// 模块的最小点数。
    /// </summary>
    public const int MinModuleDots = 2;

    /// <summary>
    /// 每个线程一支画笔（黑色、不抗锯齿），不在每次请求时创建。
    /// </summary>
    [ThreadStatic]
    private static SKPaint? threadPaint;

    /// <inheritdoc />
    public string ElementType => ElementTypes.QrCode;

    /// <inheritdoc />
    public ElementLayout Layout(LabelElement element, RenderContext context)
    {
        // 模块大小取整数个打印点：宽、高方向各能放多大取小的（加一点余量防浮点误差把整数算小）。
        var code = (QrCodeElement)element;
        var symbol = MatrixSymbols.Encode(code.Symbology, code.EcLevel, context.Bind(code.Value), element.Id);
        var box = context.Box(element);
        var quietMm = Math.Max(0, code.QuietZone ?? 0);
        var quiet = context.Dots(quietMm);
        var dots = (int)Math.Floor(Math.Min((box.Width - quiet * 2) / symbol.Columns, (box.Height - quiet * 2) / symbol.Rows) + 1e-4);
        if (dots < MinModuleDots)
        {
            var mm = (double)MinModuleDots / context.Dpi * LabelUnits.MmPerInch;
            var needWidth = Math.Ceiling((symbol.Columns * mm + quietMm * 2) * 10) / 10;
            var needHeight = Math.Ceiling((symbol.Rows * mm + quietMm * 2) * 10) / 10;
            throw new LabelRenderException(
                ErrorCodes.BarcodeTooDense,
                $"元素 {element.Id} 的{MatrixSymbols.Name(code.Symbology)}过密：{symbol.Columns}×{symbol.Rows} 个模块，框只够每个模块 {Math.Max(0, dots)} 点（至少 {MinModuleDots} 点 @{context.Dpi}dpi）。把框加大到 {needWidth}×{needHeight} mm，或缩短内容",
                element.Id);
        }

        // 符号在框里居中，左上角对齐到整点。
        var width = symbol.Columns * dots;
        var height = symbol.Rows * dots;
        var area = SKRectI.Create((int)Math.Round(box.MidX - width / 2f), (int)Math.Round(box.MidY - height / 2f), width, height);
        return new MatrixLayout(element, box, symbol, dots, area);
    }

    /// <inheritdoc />
    public void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context)
    {
        // 每行里连续的黑模块合成一个矩形画，少画很多小方块。
        var matrix = (MatrixLayout)layout;
        var paint = threadPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false, Color = SKColors.Black };
        var symbol = matrix.Symbol;
        var dots = matrix.ModuleDots;
        for (var row = 0; row < symbol.Rows; row++)
        {
            for (var column = 0; column < symbol.Columns;)
            {
                if (!symbol[column, row])
                {
                    column++;
                    continue;
                }

                var start = column;
                while (column < symbol.Columns && symbol[column, row])
                {
                    column++;
                }

                canvas.DrawRect(SKRect.Create(matrix.Area.Left + start * dots, matrix.Area.Top + row * dots, (column - start) * dots, dots), paint);
            }
        }
    }

    /// <summary>
    /// 二维码的排版结果。
    /// </summary>
    /// <param name="element">元素。</param>
    /// <param name="bounds">元素框（打印点）。</param>
    /// <param name="symbol">编码结果。</param>
    /// <param name="moduleDots">每个模块的边长（打印点，整数）。</param>
    /// <param name="area">符号占的区域（整点）。</param>
    private sealed class MatrixLayout(LabelElement element, SKRect bounds, EncodedSymbol symbol, int moduleDots, SKRectI area)
        : ElementLayout(element, bounds)
    {
        /// <summary>
        /// 编码结果。
        /// </summary>
        public EncodedSymbol Symbol { get; } = symbol;

        /// <summary>
        /// 每个模块的边长（打印点）。
        /// </summary>
        public int ModuleDots { get; } = moduleDots;

        /// <summary>
        /// 符号占的区域（整点）。
        /// </summary>
        public SKRectI Area { get; } = area;

        /// <inheritdoc />
        public override SKPoint Pivot => SymbolGeometry.Pivot(Bounds, Rotation);
    }
}
