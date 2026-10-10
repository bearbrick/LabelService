using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LabelService.Infrastructure.Persistence.Sqlite;

/// <summary>
/// 每个连接打开时设置 SQLite 的 PRAGMA（ADR 0003）。全项目只在这里写 SQLite 特有的语句（备份的 VACUUM INTO 除外，见 Hosting/BackupService）。
/// </summary>
/// <remarks>
/// WAL：读不挡写；synchronous=NORMAL：WAL 下安全且快；busy_timeout：写锁被占时等 5 秒而不是立刻报错；
/// foreign_keys：SQLite 默认不检查外键。
/// 只读连接（EF Core 检查库是否存在时用）不设 journal_mode：库还不是 WAL 时（比如手工换进来的库文件）改模式要写文件，只读连接会报错。
/// </remarks>
internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    /// <summary>
    /// 要执行的 PRAGMA。
    /// </summary>
    public const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";

    private const string ReadOnlyPragmas = "PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = PragmasFor(connection);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = PragmasFor(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string PragmasFor(DbConnection connection) =>
        new SqliteConnectionStringBuilder(connection.ConnectionString).Mode == SqliteOpenMode.ReadOnly ? ReadOnlyPragmas : Pragmas;
}
