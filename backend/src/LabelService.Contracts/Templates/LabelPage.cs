namespace LabelService.Contracts.Templates;

/// <summary>
/// 标签纸张。选中画布预设后把宽高写进这里，之后改预设不影响已有模板。
/// </summary>
public sealed class LabelPage
{
    /// <summary>
    /// 宽度（mm），保留 1 位小数。
    /// </summary>
    public double Width
    {
        get; set;
    }

    /// <summary>
    /// 高度（mm），保留 1 位小数。
    /// </summary>
    public double Height
    {
        get; set;
    }

    /// <summary>
    /// 模板默认分辨率。请求里带 dpi 时以请求为准，都没有时用 300。
    /// </summary>
    public int Dpi { get; set; } = 300;
}
