using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;

namespace LabelService.Rendering.Output;

/// <summary>
/// 按格式创建输出文档。新增格式时在这里登记。
/// </summary>
internal static class OutputDocuments
{
    /// <summary>
    /// 格式是否已实现。
    /// </summary>
    public static bool IsSupported(LabelFormat format) => format switch
    {
        LabelFormat.Png => true,
        LabelFormat.Pdf => true,
        // T-006 ZPL
        _ => false,
    };

    /// <summary>
    /// 创建输出文档。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <param name="widthDots">标签宽度（打印点，已取整）。</param>
    /// <param name="heightDots">标签高度（打印点，已取整）。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="mono">是否黑白输出。</param>
    /// <param name="page">模板画布，PDF 按它的 mm 尺寸定页面大小（不受打印点取整影响）。</param>
    /// <exception cref="NotSupportedException">格式尚未实现。</exception>
    public static OutputDocument Create(LabelFormat format, int widthDots, int heightDots, int dpi, bool mono, LabelPage page) => format switch
    {
        LabelFormat.Png => new PngOutputDocument(widthDots, heightDots, dpi, mono),
        LabelFormat.Pdf => new PdfOutputDocument(widthDots, heightDots, dpi, page),
        _ => throw new NotSupportedException($"输出格式 {format} 尚未实现。"),
    };
}
