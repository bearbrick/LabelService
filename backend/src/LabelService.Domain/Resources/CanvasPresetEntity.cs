namespace LabelService.Domain.Resources;

/// <summary>
/// 表 canvas_preset：画布预设。选中预设后宽高写进模板，之后改预设不影响已有模板。
/// </summary>
public sealed class CanvasPresetEntity
{
    /// <summary>
    /// 标签纸：卷标，按标签尺寸。
    /// </summary>
    public const string LabelPaper = "标签纸";

    /// <summary>
    /// 办公纸张：A4、A5 等，用于唛头和箱单。
    /// </summary>
    public const string OfficePaper = "办公纸张";

    /// <summary>
    /// 主键。
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// 显示名称，如“A4”“100×60”。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 类型：<see cref="LabelPaper"/> 或 <see cref="OfficePaper"/>。
    /// </summary>
    public string Kind { get; set; } = LabelPaper;

    /// <summary>
    /// 宽度，mm。
    /// </summary>
    public double WidthMm
    {
        get; set;
    }

    /// <summary>
    /// 高度，mm。
    /// </summary>
    public double HeightMm
    {
        get; set;
    }

    /// <summary>
    /// 排序，小的在前。
    /// </summary>
    public int Sort
    {
        get; set;
    }

    /// <summary>
    /// 是否在设计器里显示。
    /// </summary>
    public bool Enabled { get; set; } = true;
}
