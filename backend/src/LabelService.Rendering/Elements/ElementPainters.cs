namespace LabelService.Rendering.Elements;

/// <summary>
/// 已实现的元素绘制器。新增绘制器时在下面登记一行；没登记的元素类型渲染时跳过。
/// </summary>
internal static class ElementPainters
{
    private static readonly Dictionary<string, IElementPainter> Painters = new IElementPainter[]
    {
        new LinePainter(),
        new RectPainter(),
        // T-001 TextPainter、T-002 BarcodePainter、T-003 QrCodePainter、T-004 ImagePainter / IconPainter
    }.ToDictionary(painter => painter.ElementType, StringComparer.Ordinal);

    /// <summary>
    /// 按元素 type 查找绘制器。
    /// </summary>
    /// <returns>没实现时返回 null。</returns>
    public static IElementPainter? Find(string elementType) =>
        Painters.TryGetValue(elementType, out var painter) ? painter : null;

    /// <summary>
    /// 已实现的元素类型，供检查程序核对进度。
    /// </summary>
    public static IReadOnlyCollection<string> Implemented => Painters.Keys;
}
