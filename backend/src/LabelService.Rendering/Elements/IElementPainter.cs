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
    /// 计算元素的排版结果。数据导致无法渲染时抛 <see cref="LabelRenderException"/>。
    /// </summary>
    ElementLayout Layout(LabelElement element, RenderContext context);

    /// <summary>
    /// 在未旋转的坐标系里画出元素，旋转由调用方处理。
    /// </summary>
    void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context);
}

/// <summary>
/// 排版结果的基类。绘制器需要传更多数据（文本行、条码矩阵）时继承它。
/// </summary>
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
}
