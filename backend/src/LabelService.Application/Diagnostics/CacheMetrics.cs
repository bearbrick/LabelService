using System.Diagnostics.Metrics;
using LabelService.Application.Templates;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Application.Diagnostics;

/// <summary>
/// 指标 label_cache_hit_ratio：模板、字体、图片、条码四种缓存的命中率（进程启动以来累计），设计书第 9 节。
/// </summary>
public sealed class CacheMetrics
{
    /// <summary>
    /// 注册可观测的仪表，抓取时读当前的命中率；还没有访问过的缓存不报。
    /// </summary>
    /// <param name="meterFactory">本主机的 Meter 工厂。</param>
    /// <param name="templates">已发布模板的缓存。</param>
    public CacheMetrics(IMeterFactory meterFactory, PublishedTemplateCache templates)
    {
        var meter = meterFactory.Create(LabelMetrics.MeterName);
        (string Cache, CacheCounter Counter)[] caches =
        [
            ("templates", templates.Lookups),
            ("fonts", RenderCaches.Fonts),
            ("images", RenderCaches.Images),
            ("symbols", RenderCaches.Symbols),
        ];
        meter.CreateObservableGauge(
            "label_cache_hit_ratio",
            () => caches.Where(item => item.Counter.HitRatio is not null)
                .Select(item => new Measurement<double>(item.Counter.HitRatio!.Value, new KeyValuePair<string, object?>("cache", item.Cache))),
            description: "缓存命中率（进程启动以来）：templates 模板、fonts 字体、images 图片素材、symbols 条码编码");
    }
}
