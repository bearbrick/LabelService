using System.Globalization;
using LabelService.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LabelService.Infrastructure.Persistence;

/// <summary>
/// 启动时初始化或升级数据库（执行迁移）。SQLite 保证失败可恢复：有待执行的迁移时，先用 VACUUM INTO 备份到数据目录的 backups/；
/// 迁移或之后的数据转换出错时关闭连接、用备份覆盖回去，再抛出异常让服务不启动。
/// </summary>
/// <remarks>
/// SQLite 上部分迁移（删列时重建表、<c>PRAGMA foreign_keys = 0</c>）不在事务里执行，中途失败会留下半套结构，重试也过不去
/// （验收的故障注入：重试时 duplicate column）。所以靠升级前的完整快照恢复，不靠事务。
/// SQL Server、PostgreSQL 的迁移每个都在事务里执行，失败自动回滚，用 <see cref="MigrateAsync{TContext}"/>；它们的备份用数据库自己的工具。
/// </remarks>
public static class DatabaseUpgrade
{
    /// <summary>
    /// 升级主库 label.db：执行迁移；旧库（AddApiClients 版本）在删列之前读出分类和授权，迁移后换算成业务分组（见 <see cref="LegacyUpgrade"/>）。
    /// </summary>
    /// <param name="create">创建 DbContext（检查程序注入故障时用自己的）。</param>
    /// <param name="directory">数据目录。</param>
    /// <param name="clock">时间，写业务分组的创建时间。</param>
    /// <param name="logger">日志。</param>
    /// <returns>升级完成（或不需要升级）时完成。</returns>
    /// <exception cref="InvalidOperationException">升级失败，已恢复到升级前的备份。</exception>
    public static async Task UpgradeMainAsync(Func<Task<LabelDbContext>> create, string directory, TimeProvider clock, ILogger logger)
    {
        IReadOnlyList<string> applied;
        IReadOnlyList<string> pending;
        await using (var db = await create())
        {
            applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        }

        // 没有待执行的迁移时什么也不做（不拍快照）。
        if (pending.Count == 0)
        {
            return;
        }

        await GuardAsync(Path.Combine(directory, DataOptions.DatabaseFileName), directory, applied, pending, logger, async () =>
        {
            await using var db = await create();
            // 从没有业务分组的旧版本升级时，先读出旧的模板分类和调用方授权，迁移后换算成业务分组。
            LegacySnapshot? legacy = null;
            if (applied.Count > 0 && pending.Contains(LegacyUpgrade.AdminMigration))
            {
                await db.Database.OpenConnectionAsync();
                legacy = await LegacyUpgrade.CaptureAsync(db.Database.GetDbConnection(), applied.Contains(LegacyUpgrade.ApiClientsMigration));
            }

            await db.Database.MigrateAsync();
            if (legacy is { Templates.Count: > 0 } || legacy is { Clients.Count: > 0 })
            {
                await LegacyUpgrade.ApplyAsync(db, LegacyUpgrade.Plan(legacy), clock.GetUtcNow().UtcDateTime, logger);
            }
        });
    }

    /// <summary>
    /// 升级日志库 label-log.db：只执行迁移，同样先备份、失败恢复。
    /// </summary>
    /// <param name="create">创建 DbContext。</param>
    /// <param name="directory">数据目录。</param>
    /// <param name="logger">日志。</param>
    /// <returns>升级完成（或不需要升级）时完成。</returns>
    /// <exception cref="InvalidOperationException">升级失败，已恢复到升级前的备份。</exception>
    public static async Task UpgradeLogAsync(Func<Task<LogDbContext>> create, string directory, ILogger logger)
    {
        IReadOnlyList<string> applied;
        IReadOnlyList<string> pending;
        await using (var db = await create())
        {
            applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        }

        if (pending.Count == 0)
        {
            return;
        }

        await GuardAsync(Path.Combine(directory, DataOptions.LogDatabaseFileName), directory, applied, pending, logger, async () =>
        {
            await using var db = await create();
            await db.Database.MigrateAsync();
        });
    }

    /// <summary>
    /// SQL Server、PostgreSQL：执行待执行的迁移（每个迁移在事务里，失败自动回滚，服务不启动）。
    /// </summary>
    /// <typeparam name="TContext">主库或日志库的上下文。</typeparam>
    /// <param name="create">创建上下文。</param>
    /// <param name="name">日志里显示的库名，如“主库”。</param>
    /// <param name="logger">日志。</param>
    /// <returns>完成时完成。</returns>
    public static async Task MigrateAsync<TContext>(Func<Task<TContext>> create, string name, ILogger logger)
        where TContext : DbContext
    {
        await using var db = await create();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        logger.LogWarning("{Database}有 {Count} 个待执行的迁移（{Pending}），开始升级", name, pending.Count, string.Join("、", pending));
        await db.Database.MigrateAsync();
        logger.LogInformation("{Database}升级完成", name);
    }

    /// <summary>
    /// 每次初始化或升级均先保存快照；没有迁移记录的文件也可能含有数据，失败时恢复快照并抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <param name="database">库文件的完整路径。</param>
    /// <param name="directory">数据目录（备份放在它下面的 backups/）。</param>
    /// <param name="applied">已执行的迁移。</param>
    /// <param name="pending">待执行的迁移（写日志用）。</param>
    /// <param name="logger">日志。</param>
    /// <param name="upgrade">真正的升级动作：执行迁移和数据换算。</param>
    /// <returns>升级完成时完成。</returns>
    /// <exception cref="InvalidOperationException">升级失败，已恢复到升级前的备份。</exception>
    private static async Task GuardAsync(string database, string directory, IReadOnlyList<string> applied, IReadOnlyList<string> pending, ILogger logger, Func<Task> upgrade)
    {
        var backups = Path.Combine(directory, "backups");
        Directory.CreateDirectory(backups);
        if (!File.Exists(database))
        {
            // 首次初始化也需要可恢复的起点；只创建空库，不在备份完成前执行迁移。
            await using var empty = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
            await empty.OpenAsync();
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var backup = Path.Combine(backups, $"{Path.GetFileNameWithoutExtension(database)}-before-upgrade-{stamp}-{Guid.NewGuid():N}.db");
        SqliteConnection.ClearAllPools();
        await SqliteSnapshot.CreateAsync(database, backup, CancellationToken.None);
        if (!File.Exists(backup))
        {
            throw new IOException($"迁移前快照未生成，拒绝初始化或升级：{backup}");
        }

        logger.LogWarning("{Database} 已执行 {Applied} 个迁移，有 {Count} 个待执行的迁移（{Pending}），初始化或升级前已备份到 {Backup}", Path.GetFileName(database), applied.Count, pending.Count, string.Join("、", pending), backup);
        try
        {
            await upgrade();
        }
        catch (Exception ex)
        {
            SqliteConnection.ClearAllPools();
            Restore(backup, database);
            logger.LogCritical(ex, "{Database} 升级失败，已恢复到升级前的备份 {Backup}；服务不启动，排除原因后重新启动会再次升级", Path.GetFileName(database), backup);
            throw new InvalidOperationException($"{Path.GetFileName(database)} 升级失败，已恢复到升级前的备份：{backup}", ex);
        }

        logger.LogInformation("{Database} 升级完成，备份保留在 {Backup}", Path.GetFileName(database), backup);
    }

    /// <summary>
    /// 用备份覆盖库文件，删掉残留的 -wal、-shm（备份是完整的 WAL 模式库，不需要它们）。
    /// </summary>
    /// <param name="backup">备份文件。</param>
    /// <param name="database">要恢复的库文件。</param>
    internal static void Restore(string backup, string database)
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            File.Delete(database + suffix);
        }

        File.Copy(backup, database, overwrite: true);
    }
}
