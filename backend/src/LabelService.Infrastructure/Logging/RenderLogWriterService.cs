using System.Data.Common;
using LabelService.Application.Logs;
using LabelService.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure.Logging;

/// <summary>
/// 后台写调用日志：从 <see cref="RenderLogQueue"/> 按批取出，每批一个事务写进日志库；每天清理一次过期日志。
/// </summary>
/// <remarks>
/// 写入失败只记警告、丢掉这一批，不影响生成接口。服务停止时把队列里剩下的写完再退出。
/// </remarks>
/// <param name="queue">调用日志队列。</param>
/// <param name="repository">调用日志仓储。</param>
/// <param name="options">配置节 LabelService:RenderLog。</param>
/// <param name="clock">时间来源。</param>
/// <param name="logger">日志。</param>
public sealed class RenderLogWriterService(
    RenderLogQueue queue,
    IRenderLogRepository repository,
    IOptions<RenderLogOptions> options,
    TimeProvider clock,
    ILogger<RenderLogWriterService> logger) : BackgroundService
{
    /// <summary>
    /// 删除保留期之前的日志。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除的条数。</returns>
    public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var before = clock.GetUtcNow().UtcDateTime.AddDays(-Math.Max(1, options.Value.RetentionDays));
        return repository.DeleteBeforeAsync(before, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(WriteLoopAsync(stoppingToken), CleanupLoopAsync(stoppingToken));

    /// <summary>
    /// 有日志就按批写，直到服务停止；停止时把剩下的写完。
    /// </summary>
    /// <param name="stoppingToken">服务停止。</param>
    /// <returns>循环结束时完成。</returns>
    private async Task WriteLoopAsync(CancellationToken stoppingToken)
    {
        var batch = new List<RenderLogEntity>(options.Value.BatchSize);
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
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
    /// <param name="stoppingToken">服务停止。</param>
    /// <returns>循环结束时完成。</returns>
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
                catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or DbException)
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

    /// <summary>
    /// 把队列里现有的日志按批写完。
    /// </summary>
    /// <param name="batch">复用的批缓冲区。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>队列空了时完成。</returns>
    private async Task DrainAsync(List<RenderLogEntity> batch, CancellationToken cancellationToken)
    {
        while (true)
        {
            batch.Clear();
            while (batch.Count < options.Value.BatchSize && queue.Reader.TryRead(out var entity))
            {
                batch.Add(entity);
            }

            if (batch.Count == 0)
            {
                return;
            }

            try
            {
                await repository.AddRangeAsync(batch, cancellationToken);
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or DbException)
            {
                logger.LogWarning(ex, "调用日志写入失败，丢弃 {Count} 条", batch.Count);
            }
        }
    }
}
