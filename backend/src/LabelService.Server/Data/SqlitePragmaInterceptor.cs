using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LabelService.Server.Data;

/// <summary>
/// 每个连接打开时设置 SQLite 的 PRAGMA（ADR 0003）。全项目只在这里写 SQLite 特有的语句。
/// </summary>
/// <remarks>
/// WAL：读不挡写；synchronous=NORMAL：WAL 下安全且快；busy_timeout：写锁被占时等 5 秒而不是立刻报错；
/// foreign_keys：SQLite 默认不检查外键。
/// </remarks>
internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    /// <summary>
    /// 要执行的 PRAGMA。
    /// </summary>
    public const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
