namespace LabelService.Contracts.Protocol;

/// <summary>
/// 开放接口的取值范围和上限。部署时可配置的上限，这里是默认值。
/// </summary>
public static class RenderLimits
{
    /// <summary>
    /// 不传 dpi 且模板也没设置时使用的分辨率。
    /// </summary>
    public const int DefaultDpi = 300;

    /// <summary>
    /// 允许的分辨率。
    /// </summary>
    public static IReadOnlyList<int> AllowedDpi { get; } = [203, 300, 600];

    /// <summary>
    /// 单个请求或批量条目的最小份数。
    /// </summary>
    public const int MinCopies = 1;

    /// <summary>
    /// 单个请求或批量条目的最大份数。
    /// </summary>
    public const int MaxCopies = 100;

    /// <summary>
    /// 批量单次总张数（含份数）默认上限。
    /// </summary>
    public const int MaxBatchLabels = 500;

    /// <summary>
    /// 拼版版式最多的格子数。
    /// </summary>
    public const int MaxLayoutSlots = 500;

    /// <summary>
    /// 拼版版式纸张的最大边长（mm）。
    /// </summary>
    public const double MaxPaperMm = 1000;

    /// <summary>
    /// 模板尺寸和格子尺寸比较时允许的误差（mm），两边都按 0.1 mm 记录。
    /// </summary>
    public const double LayoutSizeToleranceMm = 0.05;

    /// <summary>
    /// image 字段解码后的最大字节数（1 MB）。
    /// </summary>
    public const int MaxImageBytes = 1024 * 1024;

    /// <summary>
    /// 请求体最大字节数（20 MB）。
    /// </summary>
    public const long MaxRequestBodyBytes = 20L * 1024 * 1024;

    /// <summary>
    /// 单张服务端处理超过这个毫秒数记为慢调用。
    /// </summary>
    public const double SlowRenderThresholdMs = 100;
}
