using LabelService.Contracts.Protocol;
using SkiaSharp;

namespace LabelService.Rendering.Output;

/// <summary>
/// 一种输出格式的文档。渲染引擎对每张标签调用 BeginLabel → 绘制 → EndLabel，最后 WriteTo。
/// </summary>
/// <remarks>
/// 画布坐标一律是打印点：位图格式按点直接分配像素；PDF 实现需要把画布缩放成 72/dpi，让绘制器无感知。
/// 每种格式一个文件，写完后在 <see cref="OutputDocuments"/> 登记。
/// </remarks>
internal abstract class OutputDocument(int widthDots, int heightDots, int dpi) : IDisposable
{
    /// <summary>
    /// 标签宽度（打印点）。
    /// </summary>
    protected int WidthDots { get; } = widthDots;

    /// <summary>
    /// 标签高度（打印点）。
    /// </summary>
    protected int HeightDots { get; } = heightDots;

    /// <summary>
    /// 分辨率。
    /// </summary>
    protected int Dpi { get; } = dpi;

    /// <summary>
    /// 输出格式。
    /// </summary>
    public abstract LabelFormat Format
    {
        get;
    }

    /// <summary>
    /// 开始一张标签，返回已清成白底的画布。
    /// </summary>
    public abstract SKCanvas BeginLabel();

    /// <summary>
    /// 结束当前标签。份数由格式自行实现（PDF 重复页、ZPL 用 ^PQ）。
    /// </summary>
    public abstract void EndLabel(int copies);

    /// <summary>
    /// 把整个文档写到流里。
    /// </summary>
    public abstract void WriteTo(Stream output);

    /// <inheritdoc />
    public virtual void Dispose()
    {
    }
}
