using LabelService.Contracts.Protocol;

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
        // T-005 PDF、T-006 ZPL
        _ => false,
    };

    /// <summary>
    /// 创建输出文档。
    /// </summary>
    /// <exception cref="NotSupportedException">格式尚未实现。</exception>
    public static OutputDocument Create(LabelFormat format, int widthDots, int heightDots, int dpi, bool mono) => format switch
    {
        LabelFormat.Png => new PngOutputDocument(widthDots, heightDots, dpi, mono),
        _ => throw new NotSupportedException($"输出格式 {format} 尚未实现。"),
    };
}
