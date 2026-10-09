using System.Diagnostics;
using System.Text;
using LabelService.Contracts.Protocol;
using LabelService.Server;
using LabelService.Server.Auth;
using LabelService.Server.Data;
using LabelService.Server.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LabelService.Backend.Checks;

/// <summary>
/// 调用方和调用日志（T-009）：Key 只存哈希、限制模板、停用和重置立即生效；调用日志的字段、异步写入、队列满丢弃、
/// 写入失败不影响生成、清理过期日志、筛选查询和索引性能。
/// </summary>
internal static class ClientLogChecks
{
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory);
            using var http = factory.CreateClient();
            var services = factory.Services;
            var (clientId, key) = await CheckClientsAsync(services, http, check);
            await CheckLogsAsync(services, http, clientId, key, check);
            await CheckLogPerformanceAsync(services, check);
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }

        CheckQueue(check);
        await CheckWriteFailureAsync(check);
    }

    private static async Task<(int ClientId, string Key)> CheckClientsAsync(IServiceProvider services, HttpClient http, Action<string, bool> check)
    {
        var repository = services.GetRequiredService<ApiClientRepository>();
        var (mes, key) = await repository.CreateAsync("MES", ["*"]);
        check("新建调用方返回 Key 原文（开发环境 lbl_test_ 前缀），可以直接调用", key.StartsWith("lbl_test_", StringComparison.Ordinal) && key.Length == 41 && await Status(http, key) == 200);
        await using (var db = await services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContextAsync())
        {
            var row = await db.ApiClients.SingleAsync(item => item.Id == mes.Id);
            check("库里只存 Key 的 SHA-256 和前缀，不存原文",
                row.ApiKeyHash == ApiKeyAuthenticator.Hash(key) && row.KeyPrefix == key[..13] && !row.ApiKeyHash.Contains(key[9..], StringComparison.Ordinal));
        }

        var (_, limitedKey) = await repository.CreateAsync("WMS", ["OTHER_LABEL"]);
        check("只允许部分模板的调用方用别的模板返回 403 TEMPLATE_FORBIDDEN", await Status(http, limitedKey) == 403);
        check("调用方名称不能重复", await Throws(() => repository.CreateAsync("MES", ["*"])));

        await repository.UpdateAsync(mes.Id, null, enabled: false);
        check("停用后立即不能调用（401）", await Status(http, key) == 401);
        await repository.UpdateAsync(mes.Id, null, enabled: true);
        var newKey = await repository.ResetKeyAsync(mes.Id);
        check("重置 Key 后旧 Key 立即失效、新 Key 可用", await Status(http, key) == 401 && await Status(http, newKey) == 200);
        check("配置文件里的开发 Key 仍然可用", await Status(http, Fixtures.DevApiKey) == 200);

        // 后面的日志检查用 MES 的新 Key 调用。
        return (mes.Id, newKey);
    }

    private static async Task CheckLogsAsync(IServiceProvider services, HttpClient http, int clientId, string key, Action<string, bool> check)
    {
        var service = services.GetRequiredService<RenderLogService>();
        var query = services.GetRequiredService<RenderLogQuery>();
        var started = DateTime.UtcNow;
        await Status(http, key, requestId: "REQ-OK-1");
        await Status(http, key, requestId: "REQ-BAD-1", data: """{"partName":"电源适配器"}""");
        var written = await WaitForLogsAsync(query, logs => logs.Any(log => log.RequestId == "REQ-BAD-1"));

        var ok = written.FirstOrDefault(log => log.RequestId == "REQ-OK-1");
        check("成功的调用写一条日志：调用方、模板、版本、格式、张数、耗时、各阶段耗时、结果",
            ok is { Endpoint: "render", ClientName: "MES", TemplateCode: "BOX_LABEL", VersionNo: 1, Format: "png", ItemCount: 1, Status: "ok", ErrorCode: null, IsSlow: false }
            && ok.ClientId == clientId && ok.DurationMs > 0 && ok.StageMsJson.Contains("\"layout\":", StringComparison.Ordinal) && ok.CreatedAt >= started.AddSeconds(-1));
        var bad = written.FirstOrDefault(log => log.RequestId == "REQ-BAD-1");
        check("失败的调用也写日志：结果 error 和错误码", bad is { Status: "error", ErrorCode: ErrorCodes.FieldRequired, ItemCount: 0 });
        check("日志里不存业务数据", !written.Any(log => $"{log.RequestId}{log.TemplateCode}{log.StageMsJson}{log.ErrorCode}".Contains("电源适配器", StringComparison.Ordinal)));

        var byStatus = await query.SearchAsync(new RenderLogFilter(Status: "error", ClientId: clientId));
        var byTemplate = await query.SearchAsync(new RenderLogFilter(From: started.AddSeconds(-1), TemplateCode: "BOX_LABEL", PageSize: 1));
        var nextPage = await query.SearchAsync(new RenderLogFilter(From: started.AddSeconds(-1), TemplateCode: "BOX_LABEL", PageSize: 1, Before: (byTemplate[0].CreatedAt, byTemplate[0].Id)));
        check("按结果、调用方、模板、时间筛选，按时间倒序翻页",
            byStatus.All(log => log.Status == "error" && log.ClientId == clientId) && byStatus.Any(log => log.RequestId == "REQ-BAD-1")
            && byTemplate.Count == 1 && nextPage.Count == 1 && nextPage[0].Id != byTemplate[0].Id && nextPage[0].CreatedAt <= byTemplate[0].CreatedAt);

        var contexts = services.GetRequiredService<IDbContextFactory<LogDbContext>>();
        await using (var db = await contexts.CreateDbContextAsync())
        {
            db.RenderLogs.Add(new RenderLogRecord { RequestId = "OLD", Endpoint = "render", ClientName = "MES", Status = "ok", CreatedAt = DateTime.UtcNow.AddDays(-200) });
            await db.SaveChangesAsync();
        }

        var deleted = await service.CleanupAsync();
        await using (var db = await contexts.CreateDbContextAsync())
        {
            check("清理超过保留期（默认 180 天）的日志，近期的保留",
                deleted >= 1 && !await db.RenderLogs.AnyAsync(log => log.RequestId == "OLD") && await db.RenderLogs.AnyAsync(log => log.RequestId == "REQ-OK-1"));
        }
    }

    /// <summary>
    /// 索引性能：灌入大量日志后按模板、调用方、结果、时间筛选取一页。默认 20 万行；设置 LABEL_LOG_PERF_ROWS=1000000 测 100 万行。
    /// </summary>
    private static async Task CheckLogPerformanceAsync(IServiceProvider services, Action<string, bool> check)
    {
        var rows = int.TryParse(Environment.GetEnvironmentVariable("LABEL_LOG_PERF_ROWS"), out var configured) ? configured : 200_000;
        var contexts = services.GetRequiredService<IDbContextFactory<LogDbContext>>();
        await using (var db = await contexts.CreateDbContextAsync())
        {
            await db.Database.OpenConnectionAsync();
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            await using var transaction = connection.BeginTransaction();
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO render_log (request_id, endpoint, client_id, client_name, template_code, format, item_count, duration_ms, stage_ms_json, is_slow, status, created_at) " +
                "VALUES ($r, 'render', $c, 'C', $t, 'png', 1, 12.3, '{}', 0, $s, $at)";
            var parameters = new[] { "$r", "$c", "$t", "$s", "$at" }.Select(name => insert.Parameters.Add(name, SqliteType.Text)).ToArray();
            var origin = DateTime.UtcNow.AddDays(-90);
            for (var i = 0; i < rows; i++)
            {
                parameters[0].Value = "P" + i;
                parameters[1].Value = i % 20;
                parameters[2].Value = "TPL_" + (i % 50);
                parameters[3].Value = i % 100 == 0 ? "error" : "ok";
                parameters[4].Value = origin.AddSeconds(i * 7776000.0 / rows).ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", System.Globalization.CultureInfo.InvariantCulture);
                await insert.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        var query = services.GetRequiredService<RenderLogQuery>();
        RenderLogFilter[] filters =
        [
            new(TemplateCode: "TPL_7"),
            new(ClientId: 3, From: DateTime.UtcNow.AddDays(-30)),
            new(Status: "error"),
            new(From: DateTime.UtcNow.AddDays(-60), To: DateTime.UtcNow.AddDays(-59)),
        ];
        var slowest = 0.0;
        foreach (var filter in filters)
        {
            await query.SearchAsync(filter);
            var started = Stopwatch.GetTimestamp();
            var page = await query.SearchAsync(filter);
            slowest = Math.Max(slowest, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (page.Count == 0)
            {
                slowest = double.MaxValue;
            }
        }

        Console.WriteLine($"INFO: 调用日志 {rows} 行，按模板、调用方、结果、时间筛选取一页，最慢 {slowest:0.0} ms");
        check($"调用日志 {rows} 行时，各种筛选取一页都在 200 ms 以内（最慢 {slowest:0.0} ms）", slowest < 200);
    }

    /// <summary>
    /// 队列满时丢弃并计数，不阻塞。
    /// </summary>
    private static void CheckQueue(Action<string, bool> check)
    {
        var writer = new RenderLogWriter(Options.Create(new RenderLogOptions { QueueCapacity = 2 }));
        var results = Enumerable.Range(0, 5).Select(i => writer.TryWrite(new RenderLogRecord { RequestId = "Q" + i })).ToArray();
        check("日志队列满时直接丢弃并计数，不阻塞请求", results.SequenceEqual([true, true, false, false, false]) && writer.Dropped == 3 && writer.Pending == 2);
    }

    /// <summary>
    /// 日志库写不进去时只记警告，后台服务不停、队列照常清空。
    /// </summary>
    private static async Task CheckWriteFailureAsync(Action<string, bool> check)
    {
        var options = new DbContextOptionsBuilder<LogDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = Path.Combine(Path.GetTempPath(), "label-no-such-dir-" + Guid.NewGuid().ToString("N"), "x.db") }.ToString())
            .Options;
        var writer = new RenderLogWriter(Options.Create(new RenderLogOptions()));
        using var service = new RenderLogService(writer, new PooledDbContextFactory<LogDbContext>(options), Options.Create(new RenderLogOptions()), TimeProvider.System, NullLogger<RenderLogService>.Instance);
        await service.StartAsync(CancellationToken.None);
        writer.TryWrite(new RenderLogRecord { RequestId = "FAIL", Endpoint = "render", Status = "ok", CreatedAt = DateTime.UtcNow });
        for (var i = 0; i < 50 && writer.Pending > 0; i++)
        {
            await Task.Delay(20);
        }

        await Task.Delay(100);
        check("日志库写入失败只记警告：后台服务继续运行，队列照常清空", writer.Pending == 0 && service.ExecuteTask is { IsFaulted: false });
        await service.StopAsync(CancellationToken.None);
    }

    private static async Task<IReadOnlyList<RenderLogRecord>> WaitForLogsAsync(RenderLogQuery query, Func<IReadOnlyList<RenderLogRecord>, bool> ready)
    {
        IReadOnlyList<RenderLogRecord> logs = [];
        for (var i = 0; i < 100; i++)
        {
            logs = await query.SearchAsync(new RenderLogFilter(PageSize: 200));
            if (ready(logs))
            {
                break;
            }

            await Task.Delay(50);
        }

        return logs;
    }

    private static async Task<int> Status(HttpClient http, string apiKey, string? requestId = null, string? data = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render")
        {
            Content = new StringContent($$"""{"templateCode":"BOX_LABEL","format":"png","data":{{data ?? Fixtures.BoxLabelData}}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(HeaderNames.ApiKey, apiKey);
        if (requestId is not null)
        {
            request.Headers.Add(HeaderNames.RequestId, requestId);
        }

        return (int)(await http.SendAsync(request)).StatusCode;
    }

    private static async Task<bool> Throws(Func<Task> action)
    {
        try
        {
            await action();
            return false;
        }
        catch (ManagementException)
        {
            return true;
        }
    }
}
