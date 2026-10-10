using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Logging;

/// <summary>
/// 后台写调用日志：从 <see cref="RenderLogWriter"/> 的队列按批取出，每批一个事务写入 label-log.db；每天清理一次过期日志。
/// </summary>
/// <remarks>
/// 写入失败只记警告、丢掉这一批，不影响生成接口。服务停止时把队列里剩下的写完再退出。
/// </remarks>
internal sealed class RenderLogService(
    RenderLogWriter writer,
    IDbContextFactory<LogDbContext> contexts,
    IOptions<RenderLogOptions> options,
    TimeProvider clock,
    ILogger<RenderLogService> logger) : BackgroundService
{
    /// <summary>
    /// 删除保留期之前的日志。返回删除的条数。
    /// </summary>
    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var before = clock.GetUtcNow().UtcDateTime.AddDays(-Math.Max(1, options.Value.RetentionDays));
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.RenderLogs.Where(log => log.CreatedAt < before).ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(WriteLoopAsync(stoppingToken), CleanupLoopAsync(stoppingToken));

    private async Task WriteLoopAsync(CancellationToken stoppingToken)
    {
        var batch = new List<RenderLogEntity>(options.Value.BatchSize);
        try
        {
            while (await writer.Reader.WaitToReadAsync(stoppingToken))
            {
                await DrainAsync(batch, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 停止时把剩下的写完。
            await DrainAsync(batch, CancellationToken.None);
        }
    }

    /// <summary>
    /// 启动时清理一次，之后每天一次。没有请求时也会清理。
    /// </summary>
    private async Task CleanupLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), clock);
        try
        {
            do
            {
                try
                {
                    var deleted = await CleanupAsync(stoppingToken);
                    if (deleted > 0)
                    {
                        logger.LogInformation("清理了 {Count} 条超过 {Days} 天的调用日志", deleted, options.Value.RetentionDays);
                    }
                }
                catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
                {
                    logger.LogWarning(ex, "清理调用日志失败");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 正常停止。
        }
    }

    private async Task DrainAsync(List<RenderLogEntity> batch, CancellationToken cancellationToken)
    {
        while (true)
        {
            batch.Clear();
            while (batch.Count < options.Value.BatchSize && writer.Reader.TryRead(out var record))
            {
                batch.Add(record);
            }

            if (batch.Count == 0)
            {
                return;
            }

            try
            {
                await using var db = await contexts.CreateDbContextAsync(cancellationToken);
                db.RenderLogs.AddRange(batch);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
            {
                logger.LogWarning(ex, "调用日志写入失败，丢弃 {Count} 条", batch.Count);
            }
        }
    }
}
