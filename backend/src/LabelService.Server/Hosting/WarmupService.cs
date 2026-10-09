using System.Diagnostics;
using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using LabelService.Server.Templates;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Hosting;

/// <summary>
/// 启动预热（T-017）：把请求路径上的代码都先走一遍，第一个真实请求就不慢。预热完成前 /health/ready 不通过。
/// </summary>
/// <remarks>
/// <para>
/// 字体、图标、素材在启动时已经加载（LoadFonts、LoadAssets）。这里解析一次请求体 JSON，再用样例值把每个已发布模板
/// 按 PNG、PDF、ZPL 各渲染一次（模板自己的 DPI），触发 JIT、Skia 和编码器的初始化、条码缓存。
/// </para>
/// <para>
/// 预热在后台线程上做，不挡启动；总时间超过 <see cref="WarmupOptions.BudgetSeconds"/> 就停下（模板很多时只预热一部分），照样标记就绪。
/// 单个模板渲染失败只写警告。
/// </para>
/// </remarks>
internal sealed class WarmupService(ITemplateStore store, ILabelRenderer renderer, WarmupState state, IOptions<WarmupOptions> options, ILogger<WarmupService> logger) : BackgroundService
{
    private const string SampleRequest = """{"templateCode":"WARMUP","format":"png","dpi":300,"copies":1,"data":{"text":"预热 Warmup 123","qty":1,"date":"2026-01-01"}}""";

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Warm(stoppingToken), stoppingToken);

    private void Warm(CancellationToken stoppingToken)
    {
        var started = Stopwatch.GetTimestamp();
        var budget = TimeSpan.FromSeconds(Math.Max(1, options.Value.BudgetSeconds));
        var rendered = 0;
        var templates = options.Value.Enabled ? store.LatestPublished() : [];
        try
        {
            JsonSerializer.Deserialize<RenderApiRequest>(SampleRequest, LabelJson.Options);
            foreach (var published in templates)
            {
                if (stoppingToken.IsCancellationRequested || Stopwatch.GetElapsedTime(started) > budget)
                {
                    logger.LogWarning("预热超过 {Budget} 秒，只预热了 {Done}/{Total} 个模板", budget.TotalSeconds, rendered, templates.Count);
                    break;
                }

                if (Render(published))
                {
                    rendered++;
                }
            }
        }
        finally
        {
            state.MarkReady(Stopwatch.GetElapsedTime(started), rendered);
            logger.LogInformation("预热完成：{Count} 个模板，用时 {Ms:0} ms", rendered, state.Duration.TotalMilliseconds);
        }
    }

    private bool Render(PublishedTemplate published)
    {
        var template = published.Template;
        var data = TemplatePublishValidator.SampleData(template.Fields, []);
        var validation = FieldValidator.Validate(template.Fields, null, data);
        if (!validation.IsValid)
        {
            logger.LogWarning("预热跳过模板 {Code}：样例值不合法", template.Code);
            return false;
        }

        foreach (var format in new[] { LabelFormat.Png, LabelFormat.Pdf, LabelFormat.Zpl })
        {
            try
            {
                renderer.Render(new RenderJob
                {
                    Template = template,
                    Labels = [new LabelInstance(validation.Values)],
                    Format = format,
                    Dpi = template.Page.Dpi > 0 ? template.Page.Dpi : RenderLimits.DefaultDpi,
                });
            }
            catch (LabelRenderException ex)
            {
                logger.LogWarning("预热模板 {Code} 的 {Format} 失败：{Code2} {Message}", template.Code, format, ex.ErrorCode, ex.Message);
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// 预热状态，/health/ready 读它。
/// </summary>
internal sealed class WarmupState
{
    private volatile bool ready;

    /// <summary>是否已经预热完成。</summary>
    public bool Ready => ready;

    /// <summary>预热用时。</summary>
    public TimeSpan Duration
    {
        get; private set;
    }

    /// <summary>预热了几个模板。</summary>
    public int Templates
    {
        get; private set;
    }

    /// <summary>
    /// 标记预热完成。
    /// </summary>
    public void MarkReady(TimeSpan duration, int templates)
    {
        Duration = duration;
        Templates = templates;
        ready = true;
    }
}

/// <summary>
/// 就绪检查：预热完成前不健康（负载均衡、部署脚本据此等待）。
/// </summary>
internal sealed class WarmupHealthCheck(WarmupState state) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.Ready
            ? HealthCheckResult.Healthy($"预热完成：{state.Templates} 个模板，{state.Duration.TotalMilliseconds:0} ms")
            : HealthCheckResult.Unhealthy("正在预热"));
}

/// <summary>
/// 配置节 LabelService:Warmup。
/// </summary>
internal sealed class WarmupOptions
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
