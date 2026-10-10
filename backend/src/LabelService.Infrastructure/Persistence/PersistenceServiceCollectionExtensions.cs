using LabelService.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure.Persistence;

/// <summary>
/// 数据库的依赖注入注册：主库、日志库的上下文工厂。
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// 注册主库和日志库。可以重复调用，只注册一次。连接字符串在第一次创建上下文时才读，检查程序覆盖的配置（如临时数据目录）能生效。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置，读 LabelService:Data 节。</param>
    /// <returns>同一个服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLabelPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<LabelDbContext>)))
        {
            return services;
        }

        services.Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SqlitePragmaInterceptor>();

        // SQLite：每个连接打开时设置 PRAGMA（WAL、外键等）。
        services.AddDbContextFactory<SqliteLabelDbContext>((provider, options) => options
            .UseSqlite(provider.GetRequiredService<IOptions<DataOptions>>().Value.ConnectionString())
            .AddInterceptors(provider.GetRequiredService<SqlitePragmaInterceptor>()));
        services.AddDbContextFactory<SqliteLogDbContext>((provider, options) => options
            .UseSqlite(provider.GetRequiredService<IOptions<DataOptions>>().Value.LogConnectionString())
            .AddInterceptors(provider.GetRequiredService<SqlitePragmaInterceptor>()));

        // 其余代码只依赖通用的上下文类型。
        services.AddSingleton<IDbContextFactory<LabelDbContext>>(provider =>
            new DbContextFactoryAdapter<LabelDbContext, SqliteLabelDbContext>(provider.GetRequiredService<IDbContextFactory<SqliteLabelDbContext>>()));
        services.AddSingleton<IDbContextFactory<LogDbContext>>(provider =>
            new DbContextFactoryAdapter<LogDbContext, SqliteLogDbContext>(provider.GetRequiredService<IDbContextFactory<SqliteLogDbContext>>()));
        return services;
    }
}
