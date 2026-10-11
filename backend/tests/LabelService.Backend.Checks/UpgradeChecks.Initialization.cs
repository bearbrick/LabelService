using LabelService.Infrastructure.Persistence;
using LabelService.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelService.Backend.Checks;

/// <summary>
/// 首次初始化也可能在非事务迁移中断；不能按“没有迁移记录”认定文件没有数据，更不能删除文件来重试。
/// </summary>
internal static partial class UpgradeChecks
{
    /// <summary>
    /// 验证空库、无迁移历史的已有库及日志库初始化失败的恢复与安全重试。
    /// </summary>
    /// <param name="check">检查结果回调。</param>
    /// <returns>全部初始化场景检查完成的任务。</returns>
    private static async Task CheckInitializationAsync(Action<string, bool> check)
    {
        await CheckFreshMainAsync(false, check);
        await CheckFreshMainAsync(true, check);
        await CheckFreshLogAsync(check);
        await CheckBackupFailureAsync(check);
    }

    /// <summary>
    /// 注入主库首次迁移故障，验证快照、原有记录保留及后续重试。
    /// </summary>
    /// <param name="hasExistingData">是否预先创建没有迁移历史的合成记录。</param>
    /// <param name="check">检查结果回调。</param>
    /// <returns>主库故障恢复检查完成的任务。</returns>
    private static async Task CheckFreshMainAsync(bool hasExistingData, Action<string, bool> check)
    {
        var directory = TempDataDirectory.Create();
        try
        {
            if (hasExistingData)
            {
                await using var seed = Open(directory);
                await Execute(seed, "CREATE TABLE user_marker (value TEXT NOT NULL); INSERT INTO user_marker VALUES ('keep-before-init')");
            }

            var fault = new FaultAfterCommand("PRAGMA foreign_keys = 0");
            InvalidOperationException? failure = null;
            try
            {
                await DatabaseUpgrade.UpgradeMainAsync(() => Task.FromResult(Context(directory, fault)), directory, TimeProvider.System, NullLogger.Instance);
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            var name = hasExistingData ? "未登记迁移但已有数据的库" : "全新主库";
            check($"{name}：首次初始化中断恢复已保存的快照，而非留下半张新表",
                fault.Thrown && failure is not null && failure.Message.Contains("已恢复到升级前的备份", StringComparison.Ordinal));
            await using (var restored = Open(directory))
            {
                var tables = await Strings(restored, "SELECT name FROM sqlite_master WHERE type = 'table'");
                check($"{name}：恢复后没有部分迁移或迁移历史，已有合成记录保留",
                    !tables.Contains("label_template") && !tables.Contains("__EFMigrationsHistory")
                    && (!hasExistingData || (await Strings(restored, "SELECT value FROM user_marker")).SequenceEqual(["keep-before-init"])));
            }

            var backups = Directory.GetFiles(Path.Combine(directory, "backups"), "label-before-upgrade-*.db");
            check($"{name}：初始化前快照保存在本数据目录，快照自身可读",
                backups.Length == 1 && Path.GetDirectoryName(backups[0]) == Path.Combine(directory, "backups")
                && await BackupHasMarkerAsync(backups[0], hasExistingData));
            var originalBackup = await File.ReadAllBytesAsync(backups[0]);
            await DatabaseUpgrade.UpgradeMainAsync(() => Task.FromResult(Context(directory, null)), directory, TimeProvider.System, NullLogger.Instance);
            var retainedBackup = await File.ReadAllBytesAsync(backups[0]);
            check($"{name}：重试使用新快照且不会覆盖中断前的备份",
                Directory.GetFiles(Path.Combine(directory, "backups"), "label-before-upgrade-*.db").Length == 2
                && originalBackup.SequenceEqual(retainedBackup));
            await using (var db = Context(directory, null))
            {
                check($"{name}：恢复后重试完成全部迁移",
                    !(await db.Database.GetPendingMigrationsAsync()).Any() && (await db.Database.GetAppliedMigrationsAsync()).Any());
            }

            await using (var migrated = Open(directory))
            {
                check($"{name}：成功后保留原有数据且不会清空其他表",
                    !hasExistingData || (await Strings(migrated, "SELECT value FROM user_marker")).SequenceEqual(["keep-before-init"]));
            }

            var count = Directory.GetFiles(Path.Combine(directory, "backups"), "*.db").Length;
            await DatabaseUpgrade.UpgradeMainAsync(() => Task.FromResult(Context(directory, null)), directory, TimeProvider.System, NullLogger.Instance);
            check($"{name}：没有待执行迁移时不新增备份", Directory.GetFiles(Path.Combine(directory, "backups"), "*.db").Length == count);
        }
        finally
        {
            TempDataDirectory.Delete(directory);
        }
    }

    /// <summary>
    /// 只读打开迁移前快照，确认不存在迁移产物且合成标记与预期一致。
    /// </summary>
    /// <param name="database">快照文件路径。</param>
    /// <param name="expected">是否应保留初始化前的合成标记。</param>
    /// <returns>快照内容符合预期时为 true。</returns>
    private static async Task<bool> BackupHasMarkerAsync(string database, bool expected)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync();
        var tables = await Strings(connection, "SELECT name FROM sqlite_master WHERE type = 'table'");
        return !tables.Contains("label_template") && !tables.Contains("__EFMigrationsHistory")
            && (expected ? (await Strings(connection, "SELECT value FROM user_marker")).SequenceEqual(["keep-before-init"]) : !tables.Contains("user_marker"));
    }

    /// <summary>
    /// 在日志库建表时注入故障，验证独立快照、无残留半套表及安全重试。
    /// </summary>
    /// <param name="check">检查结果回调。</param>
    /// <returns>日志库初始化恢复检查完成的任务。</returns>
    private static async Task CheckFreshLogAsync(Action<string, bool> check)
    {
        var directory = TempDataDirectory.Create();
        try
        {
            var fault = new FaultAfterCommand("CREATE TABLE \"render_log\"");
            LogDbContext Create(bool inject)
            {
                var options = new DbContextOptionsBuilder<SqliteLogDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, DataOptions.LogDatabaseFileName) }.ToString());
                if (inject)
                {
                    options.AddInterceptors(fault);
                }

                return new SqliteLogDbContext(options.Options);
            }

            InvalidOperationException? failure = null;
            try
            {
                await DatabaseUpgrade.UpgradeLogAsync(() => Task.FromResult(Create(true)), directory, NullLogger.Instance);
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            check("全新日志库：首次初始化故障也走快照恢复", fault.Thrown && failure is not null && failure.Message.Contains("已恢复到升级前的备份", StringComparison.Ordinal));
            check("全新日志库：有独立的初始化前备份", Directory.GetFiles(Path.Combine(directory, "backups"), "label-log-before-upgrade-*.db").Length == 1);
            await using (var restored = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, DataOptions.LogDatabaseFileName), Pooling = false }.ToString()))
            {
                await restored.OpenAsync();
                var tables = await Strings(restored, "SELECT name FROM sqlite_master WHERE type = 'table'");
                check("全新日志库：恢复后没有半套日志表或迁移历史", !tables.Contains("render_log") && !tables.Contains("__EFMigrationsHistory"));
            }

            await DatabaseUpgrade.UpgradeLogAsync(() => Task.FromResult(Create(false)), directory, NullLogger.Instance);
            await using var db = Create(false);
            check("全新日志库：恢复后可重试完成迁移", !(await db.Database.GetPendingMigrationsAsync()).Any());
        }
        finally
        {
            TempDataDirectory.Delete(directory);
        }
    }

    /// <summary>
    /// 阻止创建备份目录，确认服务拒绝迁移且原有合成数据保持完整。
    /// </summary>
    /// <param name="check">检查结果回调。</param>
    /// <returns>备份失败场景检查完成的任务。</returns>
    private static async Task CheckBackupFailureAsync(Action<string, bool> check)
    {
        var directory = TempDataDirectory.Create();
        try
        {
            await using (var seed = Open(directory))
            {
                await Execute(seed, "CREATE TABLE user_marker (value TEXT NOT NULL); INSERT INTO user_marker VALUES ('keep-before-init')");
            }

            await File.WriteAllTextAsync(Path.Combine(directory, "backups"), "synthetic file blocking the backup directory");
            var fault = new FaultAfterCommand("PRAGMA foreign_keys = 0");
            IOException? failure = null;
            try
            {
                await DatabaseUpgrade.UpgradeMainAsync(() => Task.FromResult(Context(directory, fault)), directory, TimeProvider.System, NullLogger.Instance);
            }
            catch (IOException ex)
            {
                failure = ex;
            }

            check("备份目录不可创建时：拒绝初始化，不继续迁移", failure is not null && !fault.Thrown);
            await using var connection = Open(directory);
            var tables = await Strings(connection, "SELECT name FROM sqlite_master WHERE type = 'table'");
            check("备份失败不覆盖、删除或迁移已有合成数据",
                !tables.Contains("label_template") && (await Strings(connection, "SELECT value FROM user_marker")).SequenceEqual(["keep-before-init"]));
        }
        finally
        {
            TempDataDirectory.Delete(directory);
        }
    }
}
