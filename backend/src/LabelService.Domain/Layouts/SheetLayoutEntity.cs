namespace LabelService.Domain.Layouts;

/// <summary>
/// 表 sheet_layout：拼版版式（ADR 0005）。版式不分版本，修改立即生效；开放接口从应用层的内存快照取。
/// </summary>
public sealed class SheetLayoutEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// 版式编码，全局唯一，开放接口的 layout 参数用它。
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// 显示名。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 纸张宽度，mm。
    /// </summary>
    public double PaperWidthMm
    {
        get; set;
    }

    /// <summary>
    /// 纸张高度，mm。
    /// </summary>
    public double PaperHeightMm
    {
        get; set;
    }

    /// <summary>
    /// 格子宽度，mm，等于能用这个版式的模板宽度。
    /// </summary>
    public double SlotWidthMm
    {
        get; set;
    }

    /// <summary>
    /// 格子高度，mm。
    /// </summary>
    public double SlotHeightMm
    {
        get; set;
    }

    /// <summary>
    /// 格子位置和旋转（JSON 数组），数组顺序就是填充顺序。
    /// </summary>
    public string SlotsJson { get; set; } = "[]";

    /// <summary>
    /// 打印偏移 x，mm。
    /// </summary>
    public double OffsetXMm
    {
        get; set;
    }

    /// <summary>
    /// 打印偏移 y，mm。
    /// </summary>
    public double OffsetYMm
    {
        get; set;
    }

    /// <summary>
    /// 是否画格子边框（裁切线）。
    /// </summary>
    public bool CutLines
    {
        get; set;
    }

    /// <summary>
    /// 是否启用。停用后开放接口返回 LAYOUT_NOT_FOUND。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最后修改人账号。
    /// </summary>
    public string UpdatedBy { get; set; } = "";

    /// <summary>
    /// 最后修改时间（UTC）。
    /// </summary>
    public DateTime UpdatedAt
    {
        get; set;
    }
}
