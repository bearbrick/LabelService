using Microsoft.Data.Sqlite;

namespace LabelService.Infrastructure.Persistence.Sqlite;

/// <summary>
/// SQLite 库的一致快照：用 <c>VACUUM INTO</c> 生成，服务运行时也能做。每日备份和升级前的备份都用它。
/// </summary>
public static class SqliteSnapshot
{
    /// <summary>
    /// 给库做一份一致的快照，并改成 WAL 模式（恢复时直接替换就能用）。库文件不存在时什么都不做。
    /// </summary>
    /// <param name="database">要备份的库文件。</param>
    /// <param name="snapshot">快照文件；已存在时覆盖。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>快照写完时完成。</returns>
    public static async Task CreateAsync(string database, string snapshot, CancellationToken cancellationToken)
    {
        if (!File.Exists(database))
        {
            return;
        }

        File.Delete(snapshot);

        // 不能用只读连接：VACUUM INTO 生成的文件会沿用只读打开方式的标记，恢复后服务打不开（设不了 WAL）。VACUUM INTO 不改原库。
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path";
        command.Parameters.AddWithValue("$path", snapshot);
        await command.ExecuteNonQueryAsync(cancellationToken);

        // VACUUM INTO 生成的库是回滚日志模式，改成和线上一样的 WAL，恢复时直接替换就能用。
        await using var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshot, Pooling = false }.ToString());
        await copy.OpenAsync(cancellationToken);
        await using var wal = copy.CreateCommand();
        wal.CommandText = "PRAGMA journal_mode=WAL";
        await wal.ExecuteNonQueryAsync(cancellationToken);
    }
}
