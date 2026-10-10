using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Persistence.Sqlite;

/// <summary>
/// SQLite 的日志库（数据目录里的 label-log.db）。迁移在 Sqlite/Migrations/Log。
/// </summary>
/// <param name="options">SQLite 连接的配置。</param>
public sealed class SqliteLogDbContext(DbContextOptions<SqliteLogDbContext> options) : LogDbContext(options);
