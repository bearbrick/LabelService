using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Fonts;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 文本：排版规则见 <see cref="TextLayout"/>，超出框的部分裁掉并给 TEXT_CLIPPED 警告。
/// </summary>
/// <remarks>
/// 下划线的位置和粗细和设计器（Konva）一致：行中线往下半个字号，粗细为字号的 1/15。
/// </remarks>
internal sealed class TextPainter : IElementPainter
{
    /// <summary>
    /// 每个线程一支画笔，绘制时只改颜色和抗锯齿，不在每次请求时创建。
    /// </summary>
    [ThreadStatic]
    private static SKPaint? threadPaint;

    public string ElementType => ElementTypes.Text;

    public ElementLayout Layout(LabelElement element, RenderContext context)
    {
        var text = (TextElement)element;
        var style = text.Style ?? new TextStyle();
        var box = context.Box(element);
        var face = context.Fonts.Resolve(style.FontFamily, style.Bold == true, style.Italic == true, context.Warnings);
        var block = TextLayout.Arrange(context.Bind(text.Content), style, face, box.Width, box.Height, context.Dpi);
        if (block.Clipped)
        {
            context.Warnings.Add(WarningCodes.TextClipped + ":" + Uri.EscapeDataString(element.Id));
        }

        return new TextElementLayout(element, box, face, block, RenderContext.Color(style.Color, SKColors.Black), style.Underline == true);
    }

    public void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context)
    {
        var text = (TextElementLayout)layout;
        canvas.Save();
        canvas.ClipRect(layout.Bounds);
        DrawBlock(canvas, new SKPoint(layout.Bounds.Left, layout.Bounds.Top), text.Face, text.Block, text.Color, text.Underline, context.Mono);
        canvas.Restore();
    }

    /// <summary>
    /// 画一段排好的文本。坐标原点是 <paramref name="origin"/>（排版时的框左上角），不裁切；条码下面的文字也用它。
    /// </summary>
    internal static void DrawBlock(SKCanvas canvas, SKPoint origin, FontFace face, TextBlock block, SKColor color, bool underline, bool mono)
    {
        if (block.Lines.Count == 0)
        {
            return;
        }

        var paint = threadPaint ??= new SKPaint { Style = SKPaintStyle.Fill };
        paint.Color = color;
        paint.IsAntialias = !mono;
        var size = block.FontSize;
        var scale = size / FontFace.ReferenceSize;
        var font = face.Font(mono);

        canvas.Save();
        canvas.Translate(origin.X, origin.Y);
        using (var builder = new SKTextBlobBuilder())
        {
            foreach (var line in block.Lines.Where(line => line.Glyphs.Length > 0))
            {
                var points = new SKPoint[line.Glyphs.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    points[i] = new SKPoint(line.X[i] / scale, line.Baseline / scale);
                }

                builder.AddPositionedRun(line.Glyphs, font, points);
            }

            using var blob = builder.Build();
            if (blob is not null)
            {
                canvas.Save();
                canvas.Scale(scale);
                canvas.DrawText(blob, 0, 0, paint);
                canvas.Restore();
            }
        }

        if (underline)
        {
            var thickness = size / 15;
            foreach (var line in block.Lines.Where(line => line.Width > 0))
            {
                var y = line.Top + block.LineBox / 2 + size / 2;
                canvas.DrawRect(SKRect.Create(line.Left, y - thickness / 2, line.Width, thickness), paint);
            }
        }

        canvas.Restore();
    }

    /// <summary>
    /// 文本的排版结果。
    /// </summary>
    private sealed class TextElementLayout(LabelElement element, SKRect bounds, FontFace face, TextBlock block, SKColor color, bool underline)
        : ElementLayout(element, bounds)
    {
        public FontFace Face { get; } = face;

        public TextBlock Block { get; } = block;

        public SKColor Color { get; } = color;

        public bool Underline { get; } = underline;
    }
}
