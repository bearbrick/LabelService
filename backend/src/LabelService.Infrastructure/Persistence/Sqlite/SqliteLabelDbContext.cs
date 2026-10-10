using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Persistence.Sqlite;

/// <summary>
/// SQLite 的主库（数据目录里的 label.db）。迁移在 Sqlite/Migrations/Main。
/// </summary>
/// <param name="options">SQLite 连接的配置。</param>
public sealed class SqliteLabelDbContext(DbContextOptions<SqliteLabelDbContext> options) : LabelDbContext(options);
