using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Persistence.SqlServer;

/// <summary>
/// SQL Server 的主库。迁移在 SqlServer/Migrations/Main。
/// </summary>
/// <param name="options">SQL Server 连接的配置。</param>
public sealed class SqlServerLabelDbContext(DbContextOptions<SqlServerLabelDbContext> options) : LabelDbContext(options);

/// <summary>
/// SQL Server 的调用日志库（可以和主库是同一个库，迁移记录表分开）。迁移在 SqlServer/Migrations/Log。
/// </summary>
/// <param name="options">SQL Server 连接的配置。</param>
public sealed class SqlServerLogDbContext(DbContextOptions<SqlServerLogDbContext> options) : LogDbContext(options);
