using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 线：height 为 0 是横线，width 为 0 是竖线，都不为 0 时画左上到右下的斜线。线宽以线为中心向两侧展开。
/// </summary>
internal sealed class LinePainter : IElementPainter
{
    public string ElementType => ElementTypes.Line;

    public ElementLayout Layout(LabelElement element, RenderContext context) => new(element, context.Box(element));

    public void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context)
    {
        var element = (LineElement)layout.Element;
        var stroke = element.Stroke ?? new StrokeStyle();
        if (stroke.Width <= 0)
        {
            return;
        }

        using var paint = StrokePaint.Create(stroke, context.Dots(stroke.Width), context);
        var box = layout.Bounds;
        canvas.DrawLine(box.Left, box.Top, box.Right, box.Bottom, paint);
    }
}

/// <summary>
/// 线和边框共用的画笔设置。
/// </summary>
internal static class StrokePaint
{
    /// <summary>
    /// 按线型创建画笔；虚线间隔从 mm 换算成打印点。
    /// </summary>
    public static SKPaint Create(StrokeStyle stroke, float widthDots, RenderContext context)
    {
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = widthDots,
            Color = RenderContext.Color(stroke.Color, SKColors.Black),
            IsAntialias = !context.Mono,
        };
        if (stroke.Dash is { Length: >= 2 } dash)
        {
            paint.PathEffect = SKPathEffect.CreateDash(dash.Select(context.Dots).ToArray(), 0);
        }

        return paint;
    }
}
