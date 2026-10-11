namespace LabelService.Application.Hosting;

/// <summary>
/// 配置节 LabelService:Warmup：启动预热。
/// </summary>
public sealed class WarmupOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Warmup";

    /// <summary>
    /// 是否渲染已发布模板预热；关掉时只解析一次 JSON 就标记就绪。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 预热的时间上限，秒。默认 25（验收要求 30 秒内就绪）。
    /// </summary>
    public int BudgetSeconds { get; set; } = 25;
}
