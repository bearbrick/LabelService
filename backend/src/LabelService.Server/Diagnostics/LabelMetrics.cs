using System.Diagnostics.Metrics;
using LabelService.Contracts.Protocol;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Server.Diagnostics;

/// <summary>
/// 生成服务的指标。名字和设计书第 9 节一致，导出到 Prometheus 由 T-010 接入 OpenTelemetry。
/// </summary>
/// <remarks>
/// 维度只用取值有限的字段（接口、模板、格式、DPI、结果），不要把 requestId、数据值放进维度。
/// </remarks>
internal sealed class LabelMetrics
{
    /// <summary>
    /// Meter 名称，OpenTelemetry 按这个名字订阅。
    /// </summary>
    public const string MeterName = "LabelService";

    private readonly Histogram<double> renderDuration;
    private readonly Histogram<double> stageDuration;
    private readonly Counter<long> requests;
    private readonly Counter<long> labelsGenerated;
    private readonly UpDownCounter<long> inflight;

    /// <summary>
    /// 创建全部指标。
    /// </summary>
    public LabelMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        renderDuration = meter.CreateHistogram<double>("label_render_duration_ms", description: "服务端处理一次生成请求的总耗时（毫秒）");
        stageDuration = meter.CreateHistogram<double>("label_render_stage_ms", description: "生成请求各阶段耗时（毫秒）");
        requests = meter.CreateCounter<long>("label_requests_total", description: "生成请求数");
        labelsGenerated = meter.CreateCounter<long>("labels_generated_total", description: "生成的标签张数（含份数）");
        inflight = meter.CreateUpDownCounter<long>("label_inflight_requests", description: "正在处理的生成请求数");
    }

    /// <summary>
    /// 请求开始处理。
    /// </summary>
    public void RequestStarted() => inflight.Add(1);

    /// <summary>
    /// 请求处理结束，记录耗时和结果。
    /// </summary>
    /// <param name="endpoint">接口：render、batch、preview。</param>
    /// <param name="client">调用方名称，未识别时为 anonymous。</param>
    /// <param name="templateCode">模板编码，未知时为空。</param>
    /// <param name="format">输出格式，未知时为空。</param>
    /// <param name="dpi">实际 DPI，未知时为 0。</param>
    /// <param name="result">ok 或错误码。</param>
    /// <param name="timings">分阶段耗时。</param>
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
}
