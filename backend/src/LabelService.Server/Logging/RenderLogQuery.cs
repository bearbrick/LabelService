using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Logging;

/// <summary>
/// 调用日志查询：给管理后台（T-014）按时间、模板、调用方、结果筛选，按时间倒序翻页。
/// </summary>
/// <remarks>
/// 每种筛选条件都有和时间组成的复合索引（见 <see cref="LogDbContext"/>），100 万行时单次查询在 200 ms 以内。
/// 翻页用“上一页最后一条的时间和 ID”（keyset），越往后翻越不变慢。
/// </remarks>
internal sealed class RenderLogQuery(IDbContextFactory<LogDbContext> contexts)
{
    /// <summary>
    /// 每页最多条数。
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>
    /// 查询一页日志，按时间倒序。
    /// </summary>
    public async Task<IReadOnlyList<RenderLogRecord>> SearchAsync(RenderLogFilter filter, CancellationToken cancellationToken = default)
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

        if (filter.Before is { } before)
        {
            query = query.Where(log => log.CreatedAt < before.CreatedAt || (log.CreatedAt == before.CreatedAt && log.Id < before.Id));
        }

        return await query
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(Math.Clamp(filter.PageSize, 1, MaxPageSize))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// 日志筛选条件。为 null 的条件不筛。
/// </summary>
/// <param name="From">起始时间（UTC，含）。</param>
/// <param name="To">结束时间（UTC，不含）。</param>
/// <param name="TemplateCode">模板编码。</param>
/// <param name="ClientId">调用方 ID。</param>
/// <param name="Status">结果：ok、error、aborted。</param>
/// <param name="Before">翻页：上一页最后一条的时间和 ID。</param>
/// <param name="PageSize">每页条数，最多 200。</param>
/// <param name="TemplateCodes">只看这些模板（按用户的业务范围限制）；null 表示不限。</param>
/// <param name="Search">按链路 ID、X-Request-Id、模板编码或错误码精确查找。</param>
internal sealed record RenderLogFilter(
    DateTime? From = null,
    DateTime? To = null,
    string? TemplateCode = null,
    int? ClientId = null,
    string? Status = null,
    (DateTime CreatedAt, long Id)? Before = null,
    int PageSize = 50,
    IReadOnlyCollection<string>? TemplateCodes = null,
    string? Search = null);
