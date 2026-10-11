using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 线：height 为 0 是横线，width 为 0 是竖线，都不为 0 时画左上到右下的斜线。线宽以线为中心向两侧展开。
/// </summary>
internal sealed class LinePainter : IElementPainter
{
    /// <inheritdoc />
    public string ElementType => ElementTypes.Line;

    /// <inheritdoc />
    public ElementLayout Layout(LabelElement element, RenderContext context) => new(element, context.Box(element));

    /// <inheritdoc />
    public void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context)
    {
        var element = (LineElement)layout.Element;
        var stroke = element.Stroke ?? new StrokeStyle();
        if (stroke.Width <= 0)
        {
            return;
        }

        using var paint = StrokePaint.Create(stroke, context.Dots(stroke.Width), context);
        // 横线、竖线时框的高或宽为 0，同一个 DrawLine 就够了。
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
    /// <param name="stroke">线型：颜色、虚线。</param>
    /// <param name="widthDots">线宽（打印点）。</param>
    /// <param name="context">渲染上下文（黑白输出时不抗锯齿）。</param>
    /// <returns>新画笔，用完释放。</returns>
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
