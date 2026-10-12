namespace LabelService.Application.Rendering;

/// <summary>
/// 配置节 LabelService:Render：渲染并发和排队，字段自动值用的时区。
/// </summary>
public sealed class RenderOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Render";

    /// <summary>
    /// 同时渲染的请求数；0 表示等于 CPU 核数。
    /// </summary>
    public int MaxConcurrency
    {
        get; set;
    }

    /// <summary>
    /// 排队等待的上限，毫秒；超过返回 503。默认 2000。
    /// </summary>
    public int QueueTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// 字段自动值（当前日期、时间）用的时区，如 Asia/Shanghai 或 China Standard Time；空表示服务器本机时区。Docker 容器默认是 UTC，要配置。
    /// </summary>
    public string? TimeZone
    {
        get; set;
    }
}
