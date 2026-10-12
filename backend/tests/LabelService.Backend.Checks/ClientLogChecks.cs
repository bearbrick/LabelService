using System.Diagnostics;
using System.Text;
using LabelService.Application.ApiClients;
using LabelService.Application.ApiClients.Dtos;
using LabelService.Application.Logs;
using LabelService.Contracts.Protocol;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Logging;
using LabelService.Infrastructure.Persistence;
using LabelService.Infrastructure.Persistence.Sqlite;
using LabelService.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LabelService.Backend.Checks;

/// <summary>
/// 调用方和调用日志：Key 只存哈希、限制模板、停用和重置立即生效；调用日志的字段、异步写入、队列满丢弃、
/// 写入失败不影响生成、清理过期日志、筛选查询和索引性能。
/// </summary>
internal static class ClientLogChecks
{
    /// <summary>
    /// 运行调用方和调用日志的全部检查：用临时数据目录启动服务；队列和写入失败不需要服务。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
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

    /// <summary>
    /// 调用方：新建返回 Key 原文、库里只存哈希，授权限制、名称不能重复，停用和重置 Key 立即生效。
    /// </summary>
    /// <param name="services">服务的根容器。</param>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>后面检查日志用的调用方 ID 和它的新 Key。</returns>
    private static async Task<(int ClientId, string Key)> CheckClientsAsync(IServiceProvider services, HttpClient http, Action<string, bool> check)
    {
        Task<T> Clients<T>(Func<IApiClientAppService, Task<T>> action) => AdminScope.RunAsync(services, action);
        var (mes, key) = await Clients(clients => clients.CreateAsync(new SaveApiClientDto("MES", "", "", AllBusinesses: true, [], "")));
        check("新建调用方返回 Key 原文（开发环境 lbl_test_ 前缀），可以直接调用", key.StartsWith("lbl_test_", StringComparison.Ordinal) && key.Length == 41 && await Status(http, key) == 200);
        await using (var db = await services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContextAsync())
        {
            var row = await db.ApiClients.SingleAsync(item => item.Id == mes.Id);
            check("库里只存 Key 的 SHA-256 和前缀，不存原文",
                row.ApiKeyHash == ApiKeys.Hash(key) && row.KeyPrefix == key[..13] && !row.ApiKeyHash.Contains(key[9..], StringComparison.Ordinal));
        }

        var (_, limitedKey) = await Clients(clients => clients.CreateAsync(new SaveApiClientDto("WMS", "", "", AllBusinesses: false, [], "")));
        check("没有授权这个业务分组的调用方返回 403 TEMPLATE_FORBIDDEN", await Status(http, limitedKey) == 403);
        var (_, wide) = await ServerChecks.ListTemplatesAsync(http, key);
        var (limitedStatus, limited) = await ServerChecks.ListTemplatesAsync(http, limitedKey);
        check("模板列表只列调用方有权使用的模板：全部业务的调用方有 BOX_LABEL，没授权任何分组的是空列表",
            wide?.Templates.Any(item => item.TemplateCode == "BOX_LABEL") == true && limitedStatus == 200 && limited?.Templates.Count == 0);
        check("调用方名称不能重复", await Throws(() => Clients(clients => clients.CreateAsync(new SaveApiClientDto("MES", "", "", AllBusinesses: true, [], "")))));

        await Clients(clients => clients.SetStatusAsync(mes.Id, enabled: false));
        check("停用后立即不能调用（401）", await Status(http, key) == 401);
        await Clients(clients => clients.SetStatusAsync(mes.Id, enabled: true));
        var (_, newKey) = await Clients(clients => clients.ResetKeyAsync(mes.Id));
        check("重置 Key 后旧 Key 立即失效、新 Key 可用", await Status(http, key) == 401 && await Status(http, newKey) == 200);
        check("配置文件里的开发 Key 仍然可用", await Status(http, Fixtures.DevApiKey) == 200);

        // 后面的日志检查用 MES 的新 Key 调用。
        return (mes.Id, newKey);
    }

    /// <summary>
    /// 调用日志：成功、失败各写一条，字段齐全、不存业务数据，筛选和翻页，清理过期日志。
    /// </summary>
    /// <param name="services">服务的根容器。</param>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="clientId">调用方 ID。</param>
    /// <param name="key">调用方的 Key。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckLogsAsync(IServiceProvider services, HttpClient http, int clientId, string key, Action<string, bool> check)
    {
        var service = services.GetRequiredService<RenderLogWriterService>();
        var query = services.GetRequiredService<IRenderLogRepository>();
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
            db.RenderLogs.Add(new RenderLogEntity { RequestId = "OLD", Endpoint = "render", ClientName = "MES", Status = "ok", CreatedAt = DateTime.UtcNow.AddDays(-200) });
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
    /// <param name="services">服务的根容器。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
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

        var query = services.GetRequiredService<IRenderLogRepository>();
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
    /// <param name="check">记录检查结果。</param>
    private static void CheckQueue(Action<string, bool> check)
    {
        var writer = new RenderLogQueue(Options.Create(new RenderLogOptions { QueueCapacity = 2 }));
        var results = Enumerable.Range(0, 5).Select(i => writer.TryWrite(new RenderLogEntity { RequestId = "Q" + i })).ToArray();
        check("日志队列满时直接丢弃并计数，不阻塞请求", results.SequenceEqual([true, true, false, false, false]) && writer.Dropped == 3 && writer.Pending == 2);
    }

    /// <summary>
    /// 日志库写不进去时只记警告，后台服务不停、队列照常清空。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckWriteFailureAsync(Action<string, bool> check)
    {
        var options = new DbContextOptionsBuilder<SqliteLogDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = Path.Combine(Path.GetTempPath(), "label-no-such-dir-" + Guid.NewGuid().ToString("N"), "x.db") }.ToString())
            .Options;
        var writer = new RenderLogQueue(Options.Create(new RenderLogOptions()));
        var repository = new RenderLogRepository(new DbContextFactoryAdapter<LogDbContext, SqliteLogDbContext>(new PooledDbContextFactory<SqliteLogDbContext>(options)));
        using var service = new RenderLogWriterService(writer, repository, Options.Create(new RenderLogOptions()), TimeProvider.System, NullLogger<RenderLogWriterService>.Instance);
        await service.StartAsync(CancellationToken.None);
        writer.TryWrite(new RenderLogEntity { RequestId = "FAIL", Endpoint = "render", Status = "ok", CreatedAt = DateTime.UtcNow });
        for (var i = 0; i < 50 && writer.Pending > 0; i++)
        {
            await Task.Delay(20);
        }

        await Task.Delay(100);
        check("日志库写入失败只记警告：后台服务继续运行，队列照常清空", writer.Pending == 0 && service.ExecuteTask is { IsFaulted: false });
        await service.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// 等后台服务把日志写进库（最多约 5 秒）。
    /// </summary>
    /// <param name="query">调用日志仓储。</param>
    /// <param name="ready">什么时候算写好了。</param>
    /// <returns>最近的日志（最多 200 条）。</returns>
    private static async Task<IReadOnlyList<RenderLogEntity>> WaitForLogsAsync(IRenderLogRepository query, Func<IReadOnlyList<RenderLogEntity>, bool> ready)
    {
        IReadOnlyList<RenderLogEntity> logs = [];
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

    /// <summary>
    /// 用指定的 Key 调一次单张生成。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="apiKey">API Key。</param>
    /// <param name="requestId">X-Request-Id；null 表示不传。</param>
    /// <param name="data">字段数据（JSON 对象文本）；null 时用示例箱标的完整数据。</param>
    /// <returns>HTTP 状态码。</returns>
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

    /// <summary>
    /// 执行一个操作，看它是否抛出 <see cref="UserFriendlyException"/>。
    /// </summary>
    /// <param name="action">操作。</param>
    /// <returns>抛出时为 true。</returns>
    private static async Task<bool> Throws(Func<Task> action)
    {
        try
        {
            await action();
            return false;
        }
        catch (UserFriendlyException)
        {
            return true;
        }
    }
}
