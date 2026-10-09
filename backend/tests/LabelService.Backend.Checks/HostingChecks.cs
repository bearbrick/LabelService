using System.Diagnostics;
using System.Net;
using System.Text;
using LabelService.Contracts.Protocol;
using LabelService.Server.Diagnostics;
using LabelService.Server.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LabelService.Backend.Checks;

/// <summary>
/// 运行保障（T-017）：就绪检查等预热完成、渲染并发限制和排队超时 503、每日备份的快照和保留份数、用快照恢复的演练。
/// </summary>
internal static class HostingChecks
{
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        await CheckGateAsync(check);
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            string snapshot;
            await using (var factory = new ServerFactory(backendRoot, dataDirectory))
            {
                using var http = factory.CreateClient();
                await CheckReadyAsync(factory.Services, http, check);
                await CheckBusyAsync(factory.Services, http, check);
                snapshot = await CheckBackupAsync(factory.Services, check);
            }

            await CheckRestoreAsync(backendRoot, dataDirectory, snapshot, check);
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    private static async Task CheckGateAsync(Action<string, bool> check)
    {
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meters = provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();
        using var gate = new RenderGate(Options.Create(new RenderOptions { MaxConcurrency = 1, QueueTimeoutMs = 100 }), new LabelMetrics(meters));
        var first = await gate.EnterAsync(CancellationToken.None);
        var started = Stopwatch.GetTimestamp();
        var second = await gate.EnterAsync(CancellationToken.None);
        var waited = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        first!.Dispose();
        first.Dispose();
        using var third = await gate.EnterAsync(CancellationToken.None);
        check($"渲染名额用完时排队，超时返回 null（等了 {waited:0} ms）；名额释放后可以再进，重复释放不多放名额",
            first is not null && second is null && waited >= 90 && third is not null && gate.Capacity == 1);

        using var cores = new RenderGate(Options.Create(new RenderOptions()), new LabelMetrics(meters));
        check("渲染并发数默认等于 CPU 核数，排队上限默认 2 秒", cores.Capacity == Environment.ProcessorCount && cores.TimeoutSeconds == 2);
    }

    private static async Task CheckReadyAsync(IServiceProvider services, HttpClient http, Action<string, bool> check)
    {
        var fresh = new WarmupHealthCheck(new WarmupState());
        var unhealthy = (await fresh.CheckHealthAsync(new HealthCheckContext())).Status == HealthStatus.Unhealthy;
        var live = (await http.GetAsync("/health")).StatusCode == HttpStatusCode.OK;
        var started = Stopwatch.GetTimestamp();
        HttpResponseMessage ready;
        do
        {
            ready = await http.GetAsync("/health/ready");
        }
        while (ready.StatusCode != HttpStatusCode.OK && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(30));

        var body = await ready.Content.ReadAsStringAsync();
        var state = services.GetRequiredService<WarmupState>();
        check($"预热完成前就绪检查不通过；/health 只看存活；预热把已发布模板都渲染一遍后 /health/ready 通过（{state.Templates} 个模板，{state.Duration.TotalMilliseconds:0} ms）DEBUG unhealthy={unhealthy} live={live} ready={ready.StatusCode} body={body}",
            unhealthy && live && ready.StatusCode == HttpStatusCode.OK && body.Contains("预热完成", StringComparison.Ordinal)
            && state.Templates >= 1 && state.Duration < TimeSpan.FromSeconds(30));
    }

    private static async Task CheckBusyAsync(IServiceProvider services, HttpClient http, Action<string, bool> check)
    {
        var gate = services.GetRequiredService<RenderGate>();
        var held = new List<IDisposable>();
        for (var i = 0; i < gate.Capacity; i++)
        {
            held.Add((await gate.EnterAsync(CancellationToken.None))!);
        }

        var started = Stopwatch.GetTimestamp();
        var busy = await RenderAsync(http);
        var waited = Stopwatch.GetElapsedTime(started).TotalSeconds;
        held.ForEach(lease => lease.Dispose());
        var body = await busy.Content.ReadAsStringAsync();
        check($"名额全被占用时请求排队，超过 2 秒返回 503 SERVICE_UNAVAILABLE 并带 Retry-After（等了 {waited:0.0} 秒）",
            busy.StatusCode == HttpStatusCode.ServiceUnavailable && body.Contains("SERVICE_UNAVAILABLE", StringComparison.Ordinal)
            && busy.Headers.RetryAfter is not null && waited is >= 1.9 and < 10);
        check("名额释放后恢复正常", (await RenderAsync(http)).StatusCode == HttpStatusCode.OK);
    }

    private static async Task<string> CheckBackupAsync(IServiceProvider services, Action<string, bool> check)
    {
        var backup = services.GetRequiredService<BackupService>();
        Directory.CreateDirectory(backup.Directory);
        foreach (var old in new[] { "20200101-000000", "20200102-000000", "20200103-000000", "20200104-000000", "20200105-000000", "20200106-000000", "20200107-000000" })
        {
            Directory.CreateDirectory(Path.Combine(backup.Directory, old));
        }

        var snapshot = await backup.RunAsync();
        var kept = Directory.GetDirectories(backup.Directory).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        check("备份：VACUUM INTO 生成主库和日志库快照；只保留最近 7 份，最早的被删掉",
            File.Exists(Path.Combine(snapshot, "label.db")) && File.Exists(Path.Combine(snapshot, "label-log.db"))
            && kept.Length == 7 && kept[0] == "20200102-000000" && kept[^1] == Path.GetFileName(snapshot));
        check("快照是一个完整的 WAL 模式的库（可以直接替换恢复）：里面有已发布的示例模板",
            await CountAsync(Path.Combine(snapshot, "label.db"), "SELECT COUNT(*) FROM label_template_version WHERE status = 'published'") >= 1
            && await ScalarAsync(Path.Combine(snapshot, "label.db"), "PRAGMA journal_mode") == "wal");
        return snapshot;
    }

    private static async Task CheckRestoreAsync(string backendRoot, string dataDirectory, string snapshot, Action<string, bool> check)
    {
        var database = Path.Combine(dataDirectory, "label.db");
        var presets = await CountAsync(database, "SELECT COUNT(*) FROM canvas_preset");
        await ExecuteAsync(database, "DELETE FROM canvas_preset");
        SqliteConnection.ClearAllPools();

        // 恢复：服务停着，把快照复制回数据目录，删掉旧的 -wal、-shm。
        File.Copy(Path.Combine(snapshot, "label.db"), database, overwrite: true);
        File.Delete(database + "-wal");
        File.Delete(database + "-shm");
        await using (var restored = new ServerFactory(backendRoot, dataDirectory))
        {
            using var http = restored.CreateClient();
            var render = await RenderAsync(http);
            check("恢复演练：用快照替换 label.db 后服务正常启动，被删的数据回来了，示例模板照常生成",
                presets > 0 && await CountAsync(database, "SELECT COUNT(*) FROM canvas_preset") == presets && render.StatusCode == HttpStatusCode.OK);
        }

        // 换进来的库文件不是 WAL 模式时（比如别的工具导出的），服务照样能启动并改回 WAL。
        SqliteConnection.ClearAllPools();
        await ExecuteAsync(database, "PRAGMA journal_mode=DELETE");
        await using var legacy = new ServerFactory(backendRoot, dataDirectory);
        using var client = legacy.CreateClient();
        check("库文件不是 WAL 模式时服务也能启动（只读连接不改日志模式），启动后是 WAL",
            (await RenderAsync(client)).StatusCode == HttpStatusCode.OK && await ScalarAsync(database, "PRAGMA journal_mode") == "wal");
    }

    private static Task<HttpResponseMessage> RenderAsync(HttpClient http)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render")
        {
            Content = new StringContent(File.ReadAllText(Path.Combine(Fixtures.BackendRoot(), "samples", "requests", "box-label.render.json")), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        return http.SendAsync(request);
    }

    private static async Task<long> CountAsync(string database, string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string?> ScalarAsync(string database, string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync())?.ToString();
    }

    private static async Task ExecuteAsync(string database, string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
