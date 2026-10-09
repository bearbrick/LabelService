using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 矩形：先填充，再画边框。边框画在框内侧，外沿和元素框对齐，和设计器显示一致。
/// </summary>
internal sealed class RectPainter : IElementPainter
{
    public string ElementType => ElementTypes.Rect;

    public ElementLayout Layout(LabelElement element, RenderContext context) => new(element, context.Box(element));

    public void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context)
    {
        var element = (RectElement)layout.Element;
        var radius = context.Dots(element.Radius ?? 0);
        if (!string.IsNullOrEmpty(element.Fill))
        {
            using var fill = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = RenderContext.Color(element.Fill, SKColors.Black),
                IsAntialias = !context.Mono,
            };
            canvas.DrawRoundRect(layout.Bounds, radius, radius, fill);
        }

        if (element.Stroke is not { } stroke || stroke.Width <= 0)
        {
            return;
        }

        var strokeWidth = context.Dots(stroke.Width);
        var inner = layout.Bounds;
        inner.Inflate(-strokeWidth / 2, -strokeWidth / 2);
        using var paint = StrokePaint.Create(stroke, strokeWidth, context);
        canvas.DrawRoundRect(inner, Math.Max(0, radius - strokeWidth / 2), Math.Max(0, radius - strokeWidth / 2), paint);
    }
}
