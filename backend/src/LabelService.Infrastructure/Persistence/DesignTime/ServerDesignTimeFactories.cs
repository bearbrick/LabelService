using LabelService.Infrastructure.Persistence.PostgreSql;
using LabelService.Infrastructure.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LabelService.Infrastructure.Persistence.DesignTime;

/// <summary>
/// 生成 SQL Server 主库迁移时（dotnet ef migrations add）用的上下文。生成迁移不连接数据库，连接字符串只是占位。
/// </summary>
internal sealed class SqlServerLabelDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerLabelDbContext>
{
    /// <inheritdoc />
    public SqlServerLabelDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqlServerLabelDbContext>().UseSqlServer(PersistenceProviders.DesignTimeSqlServer).Options);
}

/// <summary>
/// 生成 SQL Server 日志库迁移时用的上下文。
/// </summary>
internal sealed class SqlServerLogDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerLogDbContext>
{
    /// <inheritdoc />
    public SqlServerLogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqlServerLogDbContext>()
            .UseSqlServer(PersistenceProviders.DesignTimeSqlServer, sql => sql.MigrationsHistoryTable(PersistenceProviders.LogMigrationsHistoryTable))
            .Options);
}

/// <summary>
/// 生成 PostgreSQL 主库迁移时用的上下文。
/// </summary>
internal sealed class PostgreSqlLabelDesignTimeFactory : IDesignTimeDbContextFactory<PostgreSqlLabelDbContext>
{
    /// <inheritdoc />
    public PostgreSqlLabelDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgreSqlLabelDbContext>().UseNpgsql(PersistenceProviders.DesignTimePostgreSql).Options);
}

/// <summary>
/// 生成 PostgreSQL 日志库迁移时用的上下文。
/// </summary>
internal sealed class PostgreSqlLogDesignTimeFactory : IDesignTimeDbContextFactory<PostgreSqlLogDbContext>
{
    /// <inheritdoc />
    public PostgreSqlLogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgreSqlLogDbContext>()
            .UseNpgsql(PersistenceProviders.DesignTimePostgreSql, npgsql => npgsql.MigrationsHistoryTable(PersistenceProviders.LogMigrationsHistoryTable))
            .Options);
}
