using System.Globalization;
using LabelService.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Hosting;

/// <summary>
/// 每日备份（T-017）：用 SQLite 的 <c>VACUUM INTO</c> 给 label.db、label-log.db 生成一致的快照，连同上传的素材文件
/// 放进数据目录下的 <c>backups/yyyyMMdd-HHmmss/</c>，保留最近若干份。
/// </summary>
/// <remarks>
/// <para>
/// <c>VACUUM INTO</c> 在服务运行时也能用，得到的是某一时刻完整、紧凑的库文件，不需要再带 -wal、-shm。
/// 恢复：停服务，把快照里的文件复制回数据目录（删掉旧的 -wal、-shm），再启动，见 backend/deploy/README.md。
/// </para>
/// <para>
/// 时间按服务器本地时区；错过的备份（服务停着）不补做，下次到点再做。
/// </para>
/// </remarks>
internal sealed class BackupService(IOptions<BackupOptions> options, IOptions<DataOptions> data, TimeProvider clock, ILogger<BackupService> logger) : BackgroundService
{
    /// <summary>
    /// 快照目录名的时间格式。
    /// </summary>
    public const string StampFormat = "yyyyMMdd-HHmmss";

    /// <summary>
    /// 备份目录的绝对路径。
    /// </summary>
    public string Directory => Path.Combine(data.Value.ResolveDirectory(), options.Value.Directory);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !TimeOnly.TryParse(options.Value.Time, CultureInfo.InvariantCulture, out var time))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.Local);
            var next = now.Date + time.ToTimeSpan();
            if (next <= now.DateTime)
            {
                next = next.AddDays(1);
            }

            try
            {
                await Task.Delay(next - now.DateTime, clock, stoppingToken);
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or SqliteException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "备份失败，明天到点再试");
            }
        }
    }

    /// <summary>
    /// 立即做一份快照并清理多余的旧快照，返回快照目录。
    /// </summary>
    public async Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        var source = data.Value.ResolveDirectory();
        var stamp = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.Local).ToString(StampFormat, CultureInfo.InvariantCulture);
        var target = Path.Combine(Directory, stamp);
        System.IO.Directory.CreateDirectory(target);
        await SnapshotAsync(Path.Combine(source, DataOptions.DatabaseFileName), Path.Combine(target, DataOptions.DatabaseFileName), cancellationToken);
        if (options.Value.IncludeLogs)
        {
            await SnapshotAsync(Path.Combine(source, DataOptions.LogDatabaseFileName), Path.Combine(target, DataOptions.LogDatabaseFileName), cancellationToken);
        }

        var assets = Path.Combine(source, "assets");
        if (System.IO.Directory.Exists(assets))
        {
            System.IO.Directory.CreateDirectory(Path.Combine(target, "assets"));
            foreach (var file in System.IO.Directory.EnumerateFiles(assets))
            {
                File.Copy(file, Path.Combine(target, "assets", Path.GetFileName(file)), overwrite: true);
            }
        }

        Prune();
        logger.LogInformation("已备份到 {Target}", target);
        return target;
    }

    private static async Task SnapshotAsync(string database, string snapshot, CancellationToken cancellationToken)
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

    /// <summary>
    /// 只保留最近 <see cref="BackupOptions.Keep"/> 份（按目录名的时间排序）。
    /// </summary>
    private void Prune()
    {
        var old = new DirectoryInfo(Directory).EnumerateDirectories()
            .Where(item => DateTime.TryParseExact(item.Name, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .OrderByDescending(item => item.Name, StringComparer.Ordinal)
            .Skip(Math.Max(1, options.Value.Keep));
        foreach (var directory in old)
        {
            directory.Delete(recursive: true);
        }
    }
}

/// <summary>
/// 配置节 LabelService:Backup。
/// </summary>
internal sealed class BackupOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Backup";

    /// <summary>
    /// 是否每天自动备份。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 每天几点备份（服务器本地时间，HH:mm）。默认凌晨 2:30。
    /// </summary>
    public string Time { get; set; } = "02:30";

    /// <summary>
    /// 保留最近几份。默认 7。
    /// </summary>
    public int Keep { get; set; } = 7;

    /// <summary>
    /// 备份目录，相对于数据目录。默认 backups。可以改成绝对路径放到另一块盘（不能是网络共享盘上的数据目录，备份目录没有这个限制）。
    /// </summary>
    public string Directory { get; set; } = "backups";

    /// <summary>
    /// 是否连调用日志库一起备份（日志库可能很大）。默认 true。
    /// </summary>
    public bool IncludeLogs { get; set; } = true;
}
