using LabelService.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LabelService.Infrastructure.Persistence.DesignTime;

/// <summary>
/// 给 dotnet ef 生成 SQLite 主库的迁移用，运行时不用。命令见 backend/README.md。
/// </summary>
internal sealed class SqliteLabelDesignTimeFactory : IDesignTimeDbContextFactory<SqliteLabelDbContext>
{
    /// <inheritdoc />
    public SqliteLabelDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteLabelDbContext>().UseSqlite("Data Source=design-time.db").Options);
}

/// <summary>
/// 给 dotnet ef 生成 SQLite 日志库的迁移用，运行时不用。
/// </summary>
internal sealed class SqliteLogDesignTimeFactory : IDesignTimeDbContextFactory<SqliteLogDbContext>
{
    /// <inheritdoc />
    public SqliteLogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteLogDbContext>().UseSqlite("Data Source=design-time-log.db").Options);
}
