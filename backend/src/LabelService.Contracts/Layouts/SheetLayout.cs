namespace LabelService.Contracts.Layouts;

/// <summary>
/// 拼版版式：A4 等纸张上怎么排多张同尺寸的标签（ADR 0005）。和模板分开保存，按格子尺寸和模板匹配。规范见 docs/contracts.md“拼版版式”。
/// </summary>
/// <remarks>
/// 长度一律 mm，原点在纸张左上角，x 向右、y 向下。<see cref="Slots"/> 的顺序就是填充顺序。
/// </remarks>
public sealed class SheetLayout
{
    /// <summary>
    /// 格子允许的旋转角度（顺时针）。
    /// </summary>
    public static IReadOnlyList<int> AllowedRotations { get; } = [0, 90, 180, 270];

    /// <summary>
    /// 版式编码，全局唯一，开放接口的 <c>layout</c> 参数用它。规则和模板编码相同。
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// 显示名。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 纸张尺寸。
    /// </summary>
    public SheetSize Paper { get; set; } = new();

    /// <summary>
    /// 格子尺寸，等于能用这个版式的模板尺寸（不随格子旋转变化）。
    /// </summary>
    public SheetSize Slot { get; set; } = new();

    /// <summary>
    /// 格子，数组顺序就是填充顺序。
    /// </summary>
    public List<SheetSlot> Slots { get; set; } = [];

    /// <summary>
    /// 打印偏移，作用于整页：打印机实际位置偏右、偏下时填负数。
    /// </summary>
    public SheetOffset Offset { get; set; } = new();

    /// <summary>
    /// 是否画出格子边框，在白纸上打印时当裁切线；用预切好的不干胶纸时关掉。
    /// </summary>
    public bool CutLines
    {
        get; set;
    }

    /// <summary>
    /// 格子在纸上占的宽高：旋转 90°、270° 时是格子尺寸的宽高互换。
    /// </summary>
    public (double Width, double Height) Footprint(SheetSlot slot) =>
        slot.Rotation is 90 or 270 ? (Slot.Height, Slot.Width) : (Slot.Width, Slot.Height);
}

/// <summary>
/// 宽高（mm）。
/// </summary>
public sealed class SheetSize
{
    /// <summary>宽度（mm）。</summary>
    public double Width
    {
        get; set;
    }

    /// <summary>高度（mm）。</summary>
    public double Height
    {
        get; set;
    }
}

/// <summary>
/// 一个格子：在纸上所占矩形的左上角，和标签在格子里的旋转。
/// </summary>
public sealed class SheetSlot
{
    /// <summary>格子所占矩形左上角的 x（mm）。旋转后的格子也按它在纸上实际占的矩形算。</summary>
    public double X
    {
        get; set;
    }

    /// <summary>格子所占矩形左上角的 y（mm）。</summary>
    public double Y
    {
        get; set;
    }

    /// <summary>标签顺时针旋转的角度：0、90、180、270。</summary>
    public int Rotation
    {
        get; set;
    }
}

/// <summary>
/// 打印偏移（mm）。
/// </summary>
public sealed class SheetOffset
{
    /// <summary>向右为正。</summary>
    public double X
    {
        get; set;
    }

    /// <summary>向下为正。</summary>
    public double Y
    {
        get; set;
    }
}
