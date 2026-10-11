namespace LabelService.Application.Logs;

/// <summary>
/// 配置节 LabelService:RenderLog：调用日志的开关、队列、批量写入和保留期。
/// </summary>
public sealed class RenderLogOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:RenderLog";

    /// <summary>
    /// 是否写调用日志。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 队列上限（条）。满了丢弃。
    /// </summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>
    /// 每批最多写入的条数，一批一个事务。
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// 保留天数，默认 180 天；每天清理一次更早的日志。
    /// </summary>
    public int RetentionDays { get; set; } = 180;
}
