using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 一种元素的绘制器。每种元素一个文件，写完后在 <see cref="ElementPainters"/> 登记。
/// </summary>
/// <remarks>
/// 分两步，对应两个计时阶段：<see cref="Layout"/> 做绑定替换、文本测量、条码编码等计算（layout），
/// <see cref="Draw"/> 只把结果画到画布上（draw）。实现必须无状态、线程安全；需要缓存时用线程安全的静态缓存。
/// </remarks>
internal interface IElementPainter
{
    /// <summary>
    /// 处理的元素 type，见 <see cref="ElementTypes"/>。
    /// </summary>
    string ElementType
    {
        get;
    }

    /// <summary>
    /// 计算元素的排版结果。
    /// </summary>
    /// <param name="element">元素。</param>
    /// <param name="context">这张标签的渲染上下文。</param>
    /// <returns>排版结果，交给 <see cref="Draw"/>。</returns>
    /// <exception cref="LabelRenderException">数据导致无法渲染（如条码内容不合法）。</exception>
    ElementLayout Layout(LabelElement element, RenderContext context);

    /// <summary>
    /// 在未旋转的坐标系里画出元素，旋转由调用方处理。
    /// </summary>
    /// <param name="canvas">画布（单位是打印点）。</param>
    /// <param name="layout">这个绘制器的 <see cref="Layout"/> 返回的结果。</param>
    /// <param name="context">这张标签的渲染上下文。</param>
    void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context);
}

/// <summary>
/// 排版结果的基类。绘制器需要传更多数据（文本行、条码矩阵）时继承它。
/// </summary>
/// <param name="element">对应的元素。</param>
/// <param name="bounds">元素未旋转时的框（打印点）。</param>
internal class ElementLayout(LabelElement element, SKRect bounds)
{
    /// <summary>
    /// 对应的元素。
    /// </summary>
    public LabelElement Element { get; } = element;

    /// <summary>
    /// 元素未旋转时的框（打印点）。
    /// </summary>
    public SKRect Bounds { get; } = bounds;

    /// <summary>
    /// 绘制时的旋转角度（顺时针，度）。默认是元素的 rotation；条码会对齐到最近的直角。
    /// </summary>
    public virtual double Rotation => Element.Rotation;

    /// <summary>
    /// 旋转中心（打印点）。默认是框的中心；条码会微调到半个点上，保证转直角后条边仍落在整点。
    /// </summary>
    public virtual SKPoint Pivot => new(Bounds.MidX, Bounds.MidY);
}
