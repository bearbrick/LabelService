using System.Text.Encodings.Web;
using System.Text.Json;
using LabelService.Application.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LabelService.Api.Hosting;

/// <summary>
/// 健康检查：<c>/health</c> 是存活检查（进程能响应就 200）；<c>/health/ready</c> 是就绪检查（预热完成才 200，否则 503）。
/// </summary>
internal static class HealthEndpoints
{
    /// <summary>
    /// 就绪检查的标签：只有 /health/ready 跑这类检查，/health 只说明进程活着。
    /// </summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// 就绪检查的说明是中文，输出时不转义成 \uXXXX，方便直接看。
    /// </summary>
    private static readonly JsonSerializerOptions ReportJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// 注册健康检查：预热完成才算就绪。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>同一个服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLabelHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<WarmupHealthCheck>("warmup", tags: [ReadyTag]);
        return services;
    }

    /// <summary>
    /// 映射 /health 和 /health/ready。
    /// </summary>
    /// <param name="app">路由。</param>
    /// <returns>同一个路由，便于链式调用。</returns>
    public static IEndpointRouteBuilder MapLabelHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => !check.Tags.Contains(ReadyTag) });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteReport,
        });
        return app;
    }

    /// <summary>
    /// 就绪检查的响应：总状态和每项检查的说明。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="report">检查结果。</param>
    /// <returns>写完时完成。</returns>
    private static Task WriteReport(HttpContext http, HealthReport report)
    {
        http.Response.ContentType = "application/json; charset=utf-8";
        var body = new ReadinessReport(report.Status.ToString(), report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Description));
        return http.Response.WriteAsync(JsonSerializer.Serialize(body, ReportJson));
    }
}

/// <summary>
/// 就绪检查的响应体。
/// </summary>
/// <param name="Status">总状态：Healthy、Unhealthy。</param>
/// <param name="Checks">每项检查的说明，如 warmup → “预热完成：2 个模板，429 ms”。</param>
internal sealed record ReadinessReport(string Status, IReadOnlyDictionary<string, string?> Checks);

/// <summary>
/// 就绪检查：启动预热（<see cref="WarmupService"/>）完成才算就绪。
/// </summary>
/// <param name="state">预热状态。</param>
internal sealed class WarmupHealthCheck(WarmupState state) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.Ready
            ? HealthCheckResult.Healthy($"预热完成：{state.Templates} 个模板，{state.Duration.TotalMilliseconds:0} ms")
            : HealthCheckResult.Unhealthy("正在预热"));
}
