using LabelService.Domain.Logs;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 调用日志仓储的 EF Core 实现。每个方法用自己的日志库上下文，可以在后台服务（单例）里用。
/// </summary>
/// <remarks>
/// 每种筛选条件都有和时间组成的复合索引，100 万行时单次查询在 200 ms 以内。
/// 翻页用“上一页最后一条的时间和 ID”（keyset），越往后翻越不变慢。
/// </remarks>
/// <param name="contexts">日志库上下文的工厂。</param>
internal sealed class RenderLogRepository(IDbContextFactory<LogDbContext> contexts) : IRenderLogRepository
{
    /// <inheritdoc />
    public async Task AddRangeAsync(IReadOnlyCollection<RenderLogEntity> entities, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.RenderLogs.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteBeforeAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.RenderLogs.Where(log => log.CreatedAt < beforeUtc).ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RenderLogEntity>> SearchAsync(RenderLogFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var query = db.RenderLogs.AsNoTracking();
        if (filter.From is { } from)
        {
            query = query.Where(log => log.CreatedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(log => log.CreatedAt < to);
        }

        if (!string.IsNullOrEmpty(filter.TemplateCode))
        {
            query = query.Where(log => log.TemplateCode == filter.TemplateCode);
        }

        if (filter.TemplateCodes is { } codes)
        {
            query = query.Where(log => codes.Contains(log.TemplateCode));
        }

        // 搜索框：按链路 ID、X-Request-Id、模板编码或错误码精确查找（这几列都有索引或者很短）。
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(log => log.TraceId == search || log.RequestId == search || log.TemplateCode == search || log.ErrorCode == search);
        }

        if (filter.ClientId is { } clientId)
        {
            query = query.Where(log => log.ClientId == clientId);
        }

        if (!string.IsNullOrEmpty(filter.Status))
        {
            query = query.Where(log => log.Status == filter.Status);
        }

        // keyset 翻页：比上一页最后一条更早的（时间相同时比 ID）。
        if (filter.Before is { } before)
        {
            query = query.Where(log => log.CreatedAt < before.CreatedAt || (log.CreatedAt == before.CreatedAt && log.Id < before.Id));
        }

        return await query
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(Math.Clamp(filter.PageSize, 1, RenderLogFilter.MaxPageSize))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes, string? status = null, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var calls = Calls(db, fromUtc, toUtc, templateCodes);
        return status is null ? await calls.CountAsync(cancellationToken) : await calls.CountAsync(log => log.Status == status, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RenderLogTotals> GetTotalsAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var calls = Calls(db, fromUtc, toUtc, templateCodes);

        // 单张接口成功调用的 P95（最近秩法）：和设计书的 100 ms 目标对照。
        var single = calls.Where(log => log.Status == "ok" && log.Endpoint == "render");
        var singleCount = await single.CountAsync(cancellationToken);
        var p95 = singleCount == 0
            ? null
            : await single.OrderBy(log => log.DurationMs)
                .Select(log => (double?)log.DurationMs)
                .Skip((int)Math.Ceiling(singleCount * 0.95) - 1)
                .FirstOrDefaultAsync(cancellationToken);
        return new RenderLogTotals(
            await calls.CountAsync(cancellationToken),
            await calls.CountAsync(log => log.Status == "error", cancellationToken),
            await calls.CountAsync(log => log.IsSlow, cancellationToken),
            await calls.SumAsync(log => (long?)log.ItemCount, cancellationToken) ?? 0,
            await calls.Where(log => log.Status == "ok").AverageAsync(log => (double?)log.DurationMs, cancellationToken) ?? 0,
            p95);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateCallCount>> TopTemplatesAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        // 先投影成匿名类型再排序（数据库能翻译），取回来以后再转成记录。
        var rows = await Calls(db, fromUtc, toUtc, templateCodes)
            .GroupBy(log => log.TemplateCode)
            .Select(group => new { Code = group.Key, Calls = group.Count(), Labels = group.Sum(log => log.ItemCount) })
            .OrderByDescending(item => item.Calls)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(row => new TemplateCallCount(row.Code, row.Calls, row.Labels)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateLatency>> SlowTemplatesAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await Calls(db, fromUtc, toUtc, templateCodes)
            .Where(log => log.Status == "ok" && log.Endpoint == "render")
            .GroupBy(log => log.TemplateCode)
            .Select(group => new { Code = group.Key, Calls = group.Count(), AverageMs = group.Average(log => log.DurationMs), MaxMs = group.Max(log => log.DurationMs) })
            .OrderByDescending(item => item.AverageMs)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(row => new TemplateLatency(row.Code, row.Calls, row.AverageMs, row.MaxMs)).ToList();
    }

    /// <summary>
    /// 一段时间里的调用，按模板范围筛。
    /// </summary>
    /// <param name="db">日志库上下文。</param>
    /// <param name="fromUtc">起始时间（UTC，含）。</param>
    /// <param name="toUtc">结束时间（UTC，不含）。</param>
    /// <param name="templateCodes">只要这些模板；null 表示不限。</param>
    /// <returns>查询（还没执行）。</returns>
    private static IQueryable<RenderLogEntity> Calls(LogDbContext db, DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes)
    {
        var query = db.RenderLogs.AsNoTracking().Where(log => log.CreatedAt >= fromUtc && log.CreatedAt < toUtc);
        return templateCodes is null ? query : query.Where(log => templateCodes.Contains(log.TemplateCode));
    }
}
