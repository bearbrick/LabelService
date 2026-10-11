using LabelService.Domain.Logs;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 操作日志仓储的 EF Core 实现。每个方法用自己的日志库上下文。
/// </summary>
/// <param name="contexts">日志库上下文的工厂。</param>
internal sealed class OperationLogRepository(IDbContextFactory<LogDbContext> contexts) : IOperationLogRepository
{
    /// <inheritdoc />
    public async Task AddAsync(OperationLogEntity entity, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.OperationLogs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperationLogEntity>> SearchAsync(OperationLogFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var query = db.OperationLogs.AsNoTracking();
        if (filter.From is { } from)
        {
            query = query.Where(log => log.CreatedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(log => log.CreatedAt < to);
        }

        if (filter.UserId is { } user)
        {
            query = query.Where(log => log.UserId == user);
        }

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            query = query.Where(log => log.Type == filter.Type);
        }

        // 模糊查找：对象、说明、操作人。
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(log => log.Target.Contains(search) || log.Detail.Contains(search) || log.UserName.Contains(search));
        }

        if (filter.BeforeId is { } before)
        {
            query = query.Where(log => log.Id < before);
        }

        return await query.OrderByDescending(log => log.Id).Take(Math.Clamp(filter.PageSize, 1, 200)).ToListAsync(cancellationToken);
    }
}
