using LabelService.Application.Diagnostics;

namespace LabelService.Api.Diagnostics;

/// <summary>
/// 指标导出（ADR 0009）：<c>GET /metrics</c> 给 Prometheus 抓取。
/// </summary>
internal static class MetricsEndpoints
{
    /// <summary>
    /// 映射 /metrics，并在开始接收请求前开始监听（第一个请求的指标也能收到）。
    /// </summary>
    /// <remarks>
    /// 不需要登录（Prometheus 抓取一般不带凭据）；只应在内网开放，反向代理不要把 /metrics 转出去（见 backend/deploy/README.md）。
    /// </remarks>
    /// <param name="app">应用。</param>
    /// <returns>同一个路由，便于链式调用。</returns>
    public static IEndpointRouteBuilder MapMetrics(this WebApplication app)
    {
        var exporter = app.Services.GetRequiredService<PrometheusExporter>();
        app.Services.GetRequiredService<CacheMetrics>();
        app.MapGet("/metrics", () => Results.Text(exporter.Scrape(), "text/plain; version=0.0.4; charset=utf-8"));
        return app;
    }
}
