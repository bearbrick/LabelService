using LabelService.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// PostgreSQL 的主库。迁移在 PostgreSql/Migrations/Main。
/// </summary>
/// <param name="options">PostgreSQL 连接的配置。</param>
public sealed class PostgreSqlLabelDbContext(DbContextOptions<PostgreSqlLabelDbContext> options) : LabelDbContext(options)
{
    /// <summary>
    /// 在通用模型上加 PostgreSQL 特有的设置。
    /// </summary>
    /// <param name="modelBuilder">模型构建器。</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 画布预设的种子数据写死了 ID 1–8，PostgreSQL 的自增序列不会因此前进；新建的预设从 100 开始编号，避免撞上种子数据。
        modelBuilder.Entity<CanvasPresetEntity>().Property(item => item.Id).HasIdentityOptions(startValue: 100);
    }
}

/// <summary>
/// PostgreSQL 的调用日志库（可以和主库是同一个库，迁移记录表分开）。迁移在 PostgreSql/Migrations/Log。
/// </summary>
/// <param name="options">PostgreSQL 连接的配置。</param>
public sealed class PostgreSqlLogDbContext(DbContextOptions<PostgreSqlLogDbContext> options) : LogDbContext(options);
