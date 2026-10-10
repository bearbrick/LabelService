using LabelService.Server.Data;
using LabelService.Server.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Logging;

/// <summary>
/// 调用日志的注册和启动初始化。
/// </summary>
internal static class RenderLogSetup
{
    /// <summary>
    /// 注册日志库、写入队列、后台写入服务和查询。
    /// </summary>
    public static IServiceCollection AddRenderLog(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RenderLogOptions>(configuration.GetSection(RenderLogOptions.SectionName));
        services.Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SqlitePragmaInterceptor>();
        services.AddDbContextFactory<LogDbContext>((provider, options) => options
            .UseSqlite(provider.GetRequiredService<IOptions<DataOptions>>().Value.LogConnectionString())
            .AddInterceptors(provider.GetRequiredService<SqlitePragmaInterceptor>()));
        services.AddSingleton<RenderLogWriter>();
        services.AddSingleton<RenderLogService>();
        services.AddHostedService(provider => provider.GetRequiredService<RenderLogService>());
        services.AddSingleton<RenderLogQuery>();
        return services;
    }

    /// <summary>
    /// 启动时建日志库、执行迁移。模板来源是文件（没有数据目录）或配置关闭时不写调用日志。
    /// </summary>
    public static async Task InitializeRenderLogAsync(this WebApplication app)
    {
        var writer = app.Services.GetRequiredService<RenderLogWriter>();
        if (!writer.Enabled || app.Services.GetRequiredService<IOptions<TemplateStoreOptions>>().Value.Source == TemplateSource.Files)
        {
            writer.Enabled = false;
            return;
        }

        var directory = app.Services.GetRequiredService<IOptions<DataOptions>>().Value.ResolveDirectory();
        Directory.CreateDirectory(directory);
        var contexts = app.Services.GetRequiredService<IDbContextFactory<LogDbContext>>();
        await DatabaseUpgrade.UpgradeLogAsync(() => contexts.CreateDbContextAsync(), directory, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RenderLogSetup)));
    }
}
