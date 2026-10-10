using LabelService.Infrastructure.Persistence.PostgreSql;
using LabelService.Infrastructure.Persistence.Sqlite;
using LabelService.Infrastructure.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure.Persistence;

/// <summary>
/// 数据库的依赖注入注册：按配置的数据库种类（ADR 0010）注册主库、日志库的上下文工厂。
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// 注册主库和日志库。可以重复调用，只注册一次。
    /// </summary>
    /// <remarks>
    /// 三种数据库的上下文都注册上，用哪一种在第一次创建上下文时按配置决定；连接字符串也是那时才读，
    /// 检查程序覆盖的配置（如临时数据目录、换数据库）能生效。其余代码只依赖通用的 <see cref="LabelDbContext"/>、<see cref="LogDbContext"/>。
    /// </remarks>
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
            .UseSqlite(Data(provider).ResolveConnectionString())
            .AddInterceptors(provider.GetRequiredService<SqlitePragmaInterceptor>()));
        services.AddDbContextFactory<SqliteLogDbContext>((provider, options) => options
            .UseSqlite(Data(provider).ResolveLogConnectionString())
            .AddInterceptors(provider.GetRequiredService<SqlitePragmaInterceptor>()));

        // SQL Server、PostgreSQL：日志库可以和主库是同一个库，所以日志库的迁移记录用另一张表。
        services.AddDbContextFactory<SqlServerLabelDbContext>((provider, options) => options
            .UseSqlServer(Data(provider).ResolveConnectionString()));
        services.AddDbContextFactory<SqlServerLogDbContext>((provider, options) => options
            .UseSqlServer(Data(provider).ResolveLogConnectionString(), sql => sql.MigrationsHistoryTable(PersistenceProviders.LogMigrationsHistoryTable)));
        services.AddDbContextFactory<PostgreSqlLabelDbContext>((provider, options) => options
            .UseNpgsql(Data(provider).ResolveConnectionString()));
        services.AddDbContextFactory<PostgreSqlLogDbContext>((provider, options) => options
            .UseNpgsql(Data(provider).ResolveLogConnectionString(), npgsql => npgsql.MigrationsHistoryTable(PersistenceProviders.LogMigrationsHistoryTable)));

        services.AddSingleton(provider => Data(provider).Provider switch
        {
            DatabaseProvider.SqlServer => Adapt<LabelDbContext, SqlServerLabelDbContext>(provider),
            DatabaseProvider.PostgreSql => Adapt<LabelDbContext, PostgreSqlLabelDbContext>(provider),
            _ => Adapt<LabelDbContext, SqliteLabelDbContext>(provider),
        });
        services.AddSingleton(provider => Data(provider).Provider switch
        {
            DatabaseProvider.SqlServer => Adapt<LogDbContext, SqlServerLogDbContext>(provider),
            DatabaseProvider.PostgreSql => Adapt<LogDbContext, PostgreSqlLogDbContext>(provider),
            _ => Adapt<LogDbContext, SqliteLogDbContext>(provider),
        });
        return services;
    }

    /// <summary>
    /// 读数据库配置。
    /// </summary>
    /// <param name="provider">服务。</param>
    /// <returns>配置节 LabelService:Data。</returns>
    private static DataOptions Data(IServiceProvider provider) => provider.GetRequiredService<IOptions<DataOptions>>().Value;

    /// <summary>
    /// 把某种数据库的上下文工厂包装成通用上下文的工厂。
    /// </summary>
    /// <typeparam name="TBase">通用上下文。</typeparam>
    /// <typeparam name="TProvider">某种数据库的上下文。</typeparam>
    /// <param name="provider">服务。</param>
    /// <returns>通用上下文的工厂。</returns>
    private static IDbContextFactory<TBase> Adapt<TBase, TProvider>(IServiceProvider provider)
        where TBase : DbContext
        where TProvider : TBase =>
        new DbContextFactoryAdapter<TBase, TProvider>(provider.GetRequiredService<IDbContextFactory<TProvider>>());
}

/// <summary>
/// 多数据库共用的常量。
/// </summary>
public static class PersistenceProviders
{
    /// <summary>
    /// SQL Server、PostgreSQL 上日志库的迁移记录表（日志库和主库可能是同一个库，主库用 EF Core 默认的 __EFMigrationsHistory）。
    /// </summary>
    public const string LogMigrationsHistoryTable = "__ef_migrations_history_log";

    /// <summary>
    /// 生成 SQL Server 迁移时用的占位连接字符串（生成迁移不连接数据库）。
    /// </summary>
    internal const string DesignTimeSqlServer = "Server=(localdb)\\MSSQLLocalDB;Database=LabelServiceDesignTime;Trusted_Connection=True";

    /// <summary>
    /// 生成 PostgreSQL 迁移时用的占位连接字符串（生成迁移不连接数据库）。
    /// </summary>
    internal const string DesignTimePostgreSql = "Host=localhost;Database=labelservice_design_time";
}
