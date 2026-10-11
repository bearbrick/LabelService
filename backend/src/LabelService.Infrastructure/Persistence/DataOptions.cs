using Microsoft.Data.Sqlite;

namespace LabelService.Infrastructure.Persistence;

/// <summary>
/// 配置节 LabelService:Data：数据库和数据目录（ADR 0003、ADR 0010）。
/// </summary>
/// <remarks>
/// 默认 SQLite，库文件放在数据目录里。换成 SQL Server 或 PostgreSQL 时配置 <see cref="Provider"/> 和 <see cref="ConnectionString"/>；
/// 数据目录仍然要有：上传的素材、JWT 签名密钥放在那里。
/// </remarks>
public sealed class DataOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Data";

    /// <summary>
    /// SQLite 主库文件名：画布预设、模板、版本、素材元数据、账号。
    /// </summary>
    public const string DatabaseFileName = "label.db";

    /// <summary>
    /// SQLite 调用日志库文件名，和主库分开（ADR 0003）。
    /// </summary>
    public const string LogDatabaseFileName = "label-log.db";

    /// <summary>
    /// 数据库种类，默认 SQLite。
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>
    /// 主库的连接字符串。SQL Server、PostgreSQL 必填；SQLite 不用填（库文件在数据目录里）。不要写进仓库。
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// 调用日志库的连接字符串，可空：不填时和主库同一个库（表名不冲突，迁移记录表分开）。SQLite 不用填。
    /// </summary>
    public string LogConnectionString { get; set; } = "";

    /// <summary>
    /// 数据目录。相对路径相对于程序目录，默认程序目录下的 data/。不能放网络共享盘，也不能让两个服务进程共用。
    /// </summary>
    public string Directory { get; set; } = "data";

    /// <summary>
    /// 是否 SQLite：只有 SQLite 有连接 PRAGMA、VACUUM INTO 备份、升级前快照和旧库换算。
    /// </summary>
    public bool IsSqlite => Provider == DatabaseProvider.Sqlite;

    /// <summary>
    /// 数据目录的绝对路径。
    /// </summary>
    /// <returns>绝对路径。</returns>
    public string ResolveDirectory() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, Directory));

    /// <summary>
    /// 主库实际用的连接字符串：SQLite 指向数据目录里的 label.db，其他数据库用 <see cref="ConnectionString"/>。
    /// </summary>
    /// <returns>连接字符串。</returns>
    /// <exception cref="InvalidOperationException">SQL Server、PostgreSQL 没有配置连接字符串。</exception>
    public string ResolveConnectionString() => IsSqlite
        ? SqliteFile(DatabaseFileName)
        : !string.IsNullOrWhiteSpace(ConnectionString)
            ? ConnectionString
            : throw new InvalidOperationException($"{SectionName}:Provider 是 {Provider}，必须配置 {SectionName}:ConnectionString");

    /// <summary>
    /// 调用日志库实际用的连接字符串：SQLite 指向数据目录里的 label-log.db，其他数据库用 <see cref="LogConnectionString"/>，没配时和主库相同。
    /// </summary>
    /// <returns>连接字符串。</returns>
    /// <exception cref="InvalidOperationException">SQL Server、PostgreSQL 没有配置连接字符串。</exception>
    public string ResolveLogConnectionString() => IsSqlite
        ? SqliteFile(LogDatabaseFileName)
        : !string.IsNullOrWhiteSpace(LogConnectionString) ? LogConnectionString : ResolveConnectionString();

    /// <summary>
    /// 数据目录里一个 SQLite 库文件的连接字符串。
    /// </summary>
    /// <param name="fileName">库文件名。</param>
    /// <returns>连接字符串。</returns>
    private string SqliteFile(string fileName) =>
        new SqliteConnectionStringBuilder { DataSource = Path.Combine(ResolveDirectory(), fileName) }.ToString();
}

/// <summary>
/// 支持的数据库。
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// SQLite（默认）：库文件在数据目录里，单实例部署（ADR 0003）。
    /// </summary>
    Sqlite,

    /// <summary>
    /// Microsoft SQL Server 2016 及以上（含 LocalDB、Azure SQL）。
    /// </summary>
    SqlServer,

    /// <summary>
    /// PostgreSQL 13 及以上。
    /// </summary>
    PostgreSql,
}
