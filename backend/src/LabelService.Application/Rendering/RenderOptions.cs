namespace LabelService.Application.Rendering;

/// <summary>
/// 配置节 LabelService:Render：渲染并发和排队。
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
}
