namespace LabelService.Contracts.Protocol;

/// <summary>
/// 开放接口的取值范围和上限。部署时可配置的上限，这里是默认值。
/// </summary>
public static class RenderLimits
{
    /// <summary>不传 dpi 且模板也没设置时使用的分辨率。</summary>
    public const int DefaultDpi = 300;

    /// <summary>允许的分辨率。</summary>
    public static IReadOnlyList<int> AllowedDpi { get; } = [203, 300, 600];

    /// <summary>单个请求或批量条目的最小份数。</summary>
    public const int MinCopies = 1;

    /// <summary>单个请求或批量条目的最大份数。</summary>
    public const int MaxCopies = 100;

    /// <summary>批量单次总张数（含份数）默认上限。</summary>
    public const int MaxBatchLabels = 500;

    /// <summary>image 字段解码后的最大字节数（1 MB）。</summary>
    public const int MaxImageBytes = 1024 * 1024;

    /// <summary>请求体最大字节数（20 MB）。</summary>
    public const long MaxRequestBodyBytes = 20L * 1024 * 1024;

    /// <summary>单张服务端处理超过这个毫秒数记为慢调用。</summary>
    public const double SlowRenderThresholdMs = 100;
}
