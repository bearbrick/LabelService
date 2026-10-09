using System.Diagnostics.Metrics;
using LabelService.Rendering.Diagnostics;
using LabelService.Server.Templates;

namespace LabelService.Server.Diagnostics;

/// <summary>
/// 指标导出（T-010、ADR 0009）：<c>GET /metrics</c> 给 Prometheus 抓取。
/// </summary>
internal static class MetricsSetup
{
    /// <summary>
    /// 注册导出器和缓存命中率指标。
    /// </summary>
    public static IServiceCollection AddMetricsExport(this IServiceCollection services)
    {
        services.AddSingleton<PrometheusExporter>();
        services.AddSingleton<CacheMetrics>();
        return services;
    }

    /// <summary>
    /// 映射 /metrics，并在开始接收请求前开始监听（第一个请求的指标也能收到）。
    /// </summary>
    /// <remarks>
    /// 不需要登录（Prometheus 抓取一般不带凭据）；只应在内网开放，反向代理不要把 /metrics 转出去（见 backend/deploy/README.md）。
    /// </remarks>
    public static IEndpointRouteBuilder MapMetrics(this WebApplication app)
    {
        var exporter = app.Services.GetRequiredService<PrometheusExporter>();
        app.Services.GetRequiredService<CacheMetrics>();
        app.MapGet("/metrics", () => Results.Text(exporter.Scrape(), "text/plain; version=0.0.4; charset=utf-8"));
        return app;
    }
}

/// <summary>
/// label_cache_hit_ratio：模板、字体、图片、条码四种缓存的命中率（进程启动以来累计），设计书第 9 节。
/// </summary>
internal sealed class CacheMetrics
{
    /// <summary>
    /// 注册可观测的仪表，抓取时读当前的命中率；还没有访问过的缓存不报。
    /// </summary>
    public CacheMetrics(IMeterFactory meterFactory, ITemplateStore store)
    {
        var meter = meterFactory.Create(LabelMetrics.MeterName);
        (string Cache, CacheCounter Counter)[] caches =
        [
            ("templates", store.Lookups),
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
