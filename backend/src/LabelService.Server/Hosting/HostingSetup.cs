using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LabelService.Server.Hosting;

/// <summary>
/// 运行保障（T-017）：启动预热和就绪检查、渲染并发限制、每日备份。
/// </summary>
internal static class HostingSetup
{
    /// <summary>
    /// 就绪检查的标签：只有 /health/ready 跑这类检查，/health 只说明进程活着。
    /// </summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// 就绪检查的说明是中文，输出时不转义成 \uXXXX，方便直接看。
    /// </summary>
    private static readonly JsonSerializerOptions ReportJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 注册服务。
    /// </summary>
    public static IServiceCollection AddLabelHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<RenderOptions>(configuration.GetSection(RenderOptions.SectionName));
        services.Configure<WarmupOptions>(configuration.GetSection(WarmupOptions.SectionName));
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));
        services.AddSingleton<RenderGate>();
        services.AddSingleton<WarmupState>();
        services.AddHostedService<WarmupService>();
        services.AddSingleton<BackupService>();
        services.AddHostedService(provider => provider.GetRequiredService<BackupService>());
        services.AddHealthChecks().AddCheck<WarmupHealthCheck>("warmup", tags: [ReadyTag]);
        return services;
    }

    /// <summary>
    /// /health：存活检查（进程能响应就 200）；/health/ready：就绪检查（预热完成才 200，否则 503）。
    /// </summary>
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

    private static Task WriteReport(HttpContext http, HealthReport report)
    {
        http.Response.ContentType = "application/json; charset=utf-8";
        return http.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Description),
        }, ReportJson));
    }
}
