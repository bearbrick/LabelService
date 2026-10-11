using System.Diagnostics;
using System.Text.Json;
using LabelService.Application.Templates;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelService.Application.Hosting;

/// <summary>
/// 启动预热：把请求路径上的代码都先走一遍，第一个真实请求就不慢。预热完成前 /health/ready 不通过。
/// </summary>
/// <remarks>
/// <para>
/// 字体、图标、素材在启动时已经加载。这里解析一次请求体 JSON，再用样例值把每个已发布模板
/// 按 PNG、PDF、ZPL 各渲染一次（模板自己的 DPI），触发 JIT、Skia 和编码器的初始化、条码缓存。
/// </para>
/// <para>
/// 预热在后台线程上做，不挡启动；总时间超过 <see cref="WarmupOptions.BudgetSeconds"/> 就停下（模板很多时只预热一部分），照样标记就绪。
/// 单个模板渲染失败只写警告。
/// </para>
/// </remarks>
/// <param name="templates">已发布模板的缓存。</param>
/// <param name="renderer">渲染引擎。</param>
/// <param name="state">预热状态，就绪检查读它。</param>
/// <param name="options">配置节 LabelService:Warmup。</param>
/// <param name="logger">日志。</param>
public sealed class WarmupService(
    PublishedTemplateCache templates,
    ILabelRenderer renderer,
    WarmupState state,
    IOptions<WarmupOptions> options,
    ILogger<WarmupService> logger) : BackgroundService
{
    /// <summary>
    /// 预热时解析一次的示例请求体，让 JSON 反序列化的代码先编译好。
    /// </summary>
    private const string SampleRequest = """{"templateCode":"WARMUP","format":"png","dpi":300,"copies":1,"data":{"text":"预热 Warmup 123","qty":1,"date":"2026-01-01"}}""";

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Warm(stoppingToken), stoppingToken);

    /// <summary>
    /// 预热全部已发布模板，超时或停止时提前结束；无论怎样最后都标记就绪。
    /// </summary>
    /// <param name="stoppingToken">服务停止。</param>
    private void Warm(CancellationToken stoppingToken)
    {
        var started = Stopwatch.GetTimestamp();
        var budget = TimeSpan.FromSeconds(Math.Max(1, options.Value.BudgetSeconds));
        var rendered = 0;
        var published = options.Value.Enabled ? templates.LatestPublished() : [];
        try
        {
            JsonSerializer.Deserialize<RenderApiRequest>(SampleRequest, LabelJson.Options);
            foreach (var template in published)
            {
                if (stoppingToken.IsCancellationRequested || Stopwatch.GetElapsedTime(started) > budget)
                {
                    logger.LogWarning("预热超过 {Budget} 秒，只预热了 {Done}/{Total} 个模板", budget.TotalSeconds, rendered, published.Count);
                    break;
                }

                if (Render(template))
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

    /// <summary>
    /// 用样例值把一个模板按三种格式各渲染一次。
    /// </summary>
    /// <param name="published">已发布的模板。</param>
    /// <returns>三种格式都成功时为 true。</returns>
    private bool Render(PublishedTemplate published)
    {
        var template = published.Template;
        var data = TemplateSampleData.Create(template.Fields, []);
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
