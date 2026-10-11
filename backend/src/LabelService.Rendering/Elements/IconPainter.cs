using System.Collections.Concurrent;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 图标：从素材库取 SVG 图标（如 <c>gb191/fragile</c>），拉伸画满元素框，按 <c>color</c> 着色（默认黑色）。
/// </summary>
/// <remarks>
/// 着色用颜色滤镜把图标里所有不透明的部分换成指定颜色，适合单色图标；黑色（默认）按图标原样画。图标不存在时抛 IMAGE_INVALID。
/// 和设计器一样按框的宽高分别缩放（框不是正方形时图标会被拉伸）。
/// </remarks>
internal sealed class IconPainter : IElementPainter
{
    /// <summary>
    /// 着色用的颜色滤镜，按颜色缓存。模板里用到的颜色很少，不设上限。
    /// </summary>
    private static readonly ConcurrentDictionary<SKColor, SKColorFilter> Filters = new();

    /// <summary>
    /// 每个线程一支画笔，不在每次请求时创建。
    /// </summary>
    [ThreadStatic]
    private static SKPaint? threadPaint;

    /// <inheritdoc />
    public string ElementType => ElementTypes.Icon;

    /// <inheritdoc />
    public ElementLayout Layout(LabelElement element, RenderContext context)
    {
        var icon = (IconElement)element;
        if (!context.Assets.TryGetIcon(icon.Icon, out var picture))
        {
            throw new LabelRenderException(ErrorCodes.ImageInvalid, $"元素 {element.Id} 引用的图标 {icon.Icon} 不存在", element.Id);
        }

        return new IconLayout(element, context.Box(element), picture, RenderContext.Color(icon.Color, SKColors.Black));
    }

    /// <inheritdoc />
    public void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context)
    {
        var icon = (IconLayout)layout;
        var cull = icon.Picture.CullRect;
        if (cull.Width <= 0 || cull.Height <= 0)
        {
            return;
        }

        var paint = threadPaint ??= new SKPaint();
        // 内置图标本来就是黑色：黑色不加滤镜，PDF 里保持矢量（带颜色滤镜的画面 PDF 只能栅格化）。
        paint.ColorFilter = icon.Color == SKColors.Black ? null : Filters.GetOrAdd(icon.Color, color => SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn));
        paint.IsAntialias = !context.Mono;
        // 图标的内容区平移到原点，按框的宽高分别缩放，再移到框的位置。
        var matrix = SKMatrix.CreateTranslation(-cull.Left, -cull.Top)
            .PostConcat(SKMatrix.CreateScale(layout.Bounds.Width / cull.Width, layout.Bounds.Height / cull.Height))
            .PostConcat(SKMatrix.CreateTranslation(layout.Bounds.Left, layout.Bounds.Top));
        canvas.Save();
        canvas.ClipRect(layout.Bounds);
        canvas.DrawPicture(icon.Picture, in matrix, paint);
        canvas.Restore();
    }

    /// <summary>
    /// 图标的排版结果。
    /// </summary>
    /// <param name="element">元素。</param>
    /// <param name="bounds">元素框（打印点）。</param>
    /// <param name="picture">图标的矢量图。</param>
    /// <param name="color">着色。</param>
    private sealed class IconLayout(LabelElement element, SKRect bounds, SKPicture picture, SKColor color) : ElementLayout(element, bounds)
    {
        /// <summary>
        /// 图标的矢量图（素材库共用，不释放）。
        /// </summary>
        public SKPicture Picture { get; } = picture;

        /// <summary>
        /// 着色；黑色时按图标原样画。
        /// </summary>
        public SKColor Color { get; } = color;
    }
}
