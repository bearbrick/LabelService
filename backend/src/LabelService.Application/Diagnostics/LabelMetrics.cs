using System.Diagnostics.Metrics;
using LabelService.Contracts.Protocol;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Application.Diagnostics;

/// <summary>
/// 生成服务的指标（设计书第 9 节），由接口层的 /metrics 导出成 Prometheus 文本格式（ADR 0009）。
/// </summary>
/// <remarks>
/// 维度只用取值有限的字段（接口、模板、格式、DPI、结果），不要把 requestId、数据值放进维度。
/// </remarks>
public sealed class LabelMetrics
{
    /// <summary>
    /// Meter 名称，导出器按这个名字订阅。
    /// </summary>
    public const string MeterName = "LabelService";

    /// <summary>
    /// 一次生成请求的总耗时。
    /// </summary>
    private readonly Histogram<double> renderDuration;

    /// <summary>
    /// 生成请求各阶段的耗时。
    /// </summary>
    private readonly Histogram<double> stageDuration;

    /// <summary>
    /// 生成请求数。
    /// </summary>
    private readonly Counter<long> requests;

    /// <summary>
    /// 生成的标签张数（含份数）。
    /// </summary>
    private readonly Counter<long> labelsGenerated;

    /// <summary>
    /// 正在处理的生成请求数。
    /// </summary>
    private readonly UpDownCounter<long> inflight;

    /// <summary>
    /// 调用日志队列满被丢弃的条数。
    /// </summary>
    private readonly Counter<long> logDropped;

    /// <summary>
    /// 等渲染名额的时间。
    /// </summary>
    private readonly Histogram<double> queueWait;

    /// <summary>
    /// 创建全部指标。
    /// </summary>
    /// <param name="meterFactory">本主机的 Meter 工厂（同一进程里多个主机的指标互不串）。</param>
    public LabelMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        renderDuration = meter.CreateHistogram<double>("label_render_duration_ms", description: "服务端处理一次生成请求的总耗时（毫秒）");
        stageDuration = meter.CreateHistogram<double>("label_render_stage_ms", description: "生成请求各阶段耗时（毫秒）");
        requests = meter.CreateCounter<long>("label_requests_total", description: "生成请求数");
        labelsGenerated = meter.CreateCounter<long>("labels_generated_total", description: "生成的标签张数（含份数）");
        inflight = meter.CreateUpDownCounter<long>("label_inflight_requests", description: "正在处理的生成请求数");
        logDropped = meter.CreateCounter<long>("label_log_dropped_total", description: "调用日志队列满被丢弃的条数");
        queueWait = meter.CreateHistogram<double>("label_queue_wait_ms", description: "生成请求等渲染名额的时间（毫秒）");
    }

    /// <summary>
    /// 请求开始处理。
    /// </summary>
    public void RequestStarted() => inflight.Add(1);

    /// <summary>
    /// 记录一次排队等待渲染名额的时间。
    /// </summary>
    /// <param name="milliseconds">等待时间，毫秒。</param>
    public void QueueWaited(double milliseconds) => queueWait.Record(milliseconds);

    /// <summary>
    /// 调用日志队列满，丢弃了一条。
    /// </summary>
    public void RenderLogDropped() => logDropped.Add(1);

    /// <summary>
    /// 请求处理结束，记录耗时和结果。
    /// </summary>
    /// <param name="endpoint">接口：render、batch。</param>
    /// <param name="client">调用方名称，未识别时为 anonymous。</param>
    /// <param name="templateCode">模板编码，未知时为空。</param>
    /// <param name="format">输出格式，未知时为空。</param>
    /// <param name="dpi">实际 DPI，未知时为 0。</param>
    /// <param name="result">ok 或错误码。</param>
    /// <param name="timings">分阶段耗时，total 是到写完最后一个字节的总耗时。</param>
    /// <param name="labelCount">生成张数，失败时为 0。</param>
    public void RequestFinished(string endpoint, string client, string templateCode, string format, int dpi, string result, RenderTimings timings, int labelCount)
    {
        inflight.Add(-1);
        requests.Add(1, new("endpoint", endpoint), new("client", client), new("result", result));
        renderDuration.Record(
            timings.Get(TimingStages.Total),
            new("endpoint", endpoint),
            new("template", templateCode),
            new("format", format),
            new("dpi", dpi),
            new("result", result));

        // 各阶段单独记，total 已经记在上面。
        foreach (var (stage, milliseconds) in timings.Stages)
        {
            if (stage != TimingStages.Total)
            {
                stageDuration.Record(milliseconds, new("stage", stage), new("format", format));
            }
        }

        if (labelCount > 0)
        {
            labelsGenerated.Add(labelCount, new("template", templateCode), new("format", format));
        }
    }

    /// <summary>
    /// 设计器的服务端预览（管理接口）结束：只记请求数和总耗时，endpoint 维度是 preview；
    /// 不记阶段耗时和生成张数，开放接口的指标不受影响。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="result">ok 或错误码。</param>
    /// <param name="totalMs">总耗时，毫秒。</param>
    public void PreviewFinished(string templateCode, int dpi, string result, double totalMs)
    {
        requests.Add(1, new("endpoint", "preview"), new("client", "designer"), new("result", result));
        renderDuration.Record(
            totalMs,
            new("endpoint", "preview"),
            new("template", templateCode),
            new("format", "png"),
            new("dpi", dpi),
            new("result", result));
    }
}
