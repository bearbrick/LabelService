using LabelService.Domain.Logs;
using LabelService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Persistence;

/// <summary>
/// 日志库：调用日志和后台操作日志。
/// </summary>
/// <remarks>
/// SQLite 下是单独的文件 label-log.db，日志批量写入不会和后台保存模板抢写锁（ADR 0003）；
/// SQL Server、PostgreSQL 下可以和主库同一个库（表名不冲突，迁移记录表分开），也可以单独配置连接字符串（ADR 0010）。
/// </remarks>
public abstract class LogDbContext : DbContext
{
    /// <summary>
    /// 由派生类传入具体数据库的配置。
    /// </summary>
    /// <param name="options">数据库连接和提供程序的配置。</param>
    protected LogDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// 调用日志。
    /// </summary>
    public DbSet<RenderLogEntity> RenderLogs => Set<RenderLogEntity>();

    /// <summary>
    /// 后台操作日志。
    /// </summary>
    public DbSet<OperationLogEntity> OperationLogs => Set<OperationLogEntity>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        ModelConventions.UseUtcDateTimes(configurationBuilder);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RenderLogConfiguration());
        modelBuilder.ApplyConfiguration(new OperationLogConfiguration());
        ModelConventions.UseSnakeCaseColumns(modelBuilder);
    }
}
