using LabelService.Server.Data;
using LabelService.Server.Logging;
using LabelService.Server.Templates;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Admin;

/// <summary>
/// 调用日志、操作日志（log.view）和概览。调用日志按用户的业务范围只看自己范围里的模板。
/// </summary>
internal static class LogEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/call-logs", SearchCallLogsAsync).RequireAdmin(Permissions.LogView);
        api.MapGet("/op-logs", SearchOpLogsAsync).RequireAdmin(Permissions.LogView);
        api.MapGet("/dashboard", DashboardAsync).RequireAdmin(Permissions.TemplateView);
    }

    private static async Task<IResult> SearchCallLogsAsync(
        HttpContext http,
        RenderLogQuery query,
        TemplateRepository templates,
        DateTime? from,
        DateTime? to,
        string? templateCode,
        int? clientId,
        string? status,
        string? q,
        DateTime? beforeTime,
        long? beforeId,
        int? pageSize)
    {
        var admin = http.CurrentAdmin();
        IReadOnlyCollection<string>? scope = admin.AllBusinesses
            ? null
            : (await templates.ListAsync()).Where(item => admin.InScope(item.BusinessId)).Select(item => item.Code).ToArray();
        var logs = await query.SearchAsync(new RenderLogFilter(
            From: from?.ToUniversalTime(),
            To: to?.ToUniversalTime(),
            TemplateCode: string.IsNullOrWhiteSpace(templateCode) ? null : templateCode,
            ClientId: clientId,
            Status: string.IsNullOrWhiteSpace(status) ? null : status,
            Before: beforeTime is { } time && beforeId is { } id ? (time.ToUniversalTime(), id) : null,
            PageSize: pageSize ?? 50,
            TemplateCodes: scope,
            Search: q));
        return Results.Json(logs);
    }

    private static async Task<IResult> SearchOpLogsAsync(
        IDbContextFactory<LogDbContext> contexts,
        DateTime? from,
        DateTime? to,
        int? userId,
        string? type,
        string? q,
        long? beforeId,
        int? pageSize)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var query = db.OpLogs.AsNoTracking();
        if (from is { } start)
        {
            var utc = start.ToUniversalTime();
            query = query.Where(log => log.CreatedAt >= utc);
        }

        if (to is { } end)
        {
            var utc = end.ToUniversalTime();
            query = query.Where(log => log.CreatedAt < utc);
        }

        if (userId is { } user)
        {
            query = query.Where(log => log.UserId == user);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(log => log.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            query = query.Where(log => log.Target.Contains(search) || log.Detail.Contains(search) || log.UserName.Contains(search));
        }

        if (beforeId is { } before)
        {
            query = query.Where(log => log.Id < before);
        }

        var logs = await query.OrderByDescending(log => log.Id).Take(Math.Clamp(pageSize ?? 50, 1, 200)).ToListAsync();
        return Results.Json(logs);
    }

    /// <summary>
    /// 概览：模板数量（按业务范围）；有 log.view 时再给今天的调用（含单张 P95）、近 14 天每天的调用量和失败数、近 7 天调用最多和最慢的模板、最近的操作。
    /// </summary>
    private static async Task<IResult> DashboardAsync(
        HttpContext http,
        TemplateRepository templates,
        IDbContextFactory<LogDbContext> logs,
        TimeProvider clock)
    {
        var admin = http.CurrentAdmin();
        var visible = (await templates.ListAsync()).Where(item => admin.InScope(item.BusinessId)).ToList();
        var templateStats = new
        {
            total = visible.Count,
            published = visible.Count(item => item.LatestVersion > 0),
            changed = visible.Count(item => item.LatestVersion > 0 && item.HasDraft),
            neverPublished = visible.Count(item => item.LatestVersion == 0),
        };
        if (!admin.Can(Permissions.LogView))
        {
            return Results.Json(new
            {
                templates = templateStats
            });
        }

        var codes = admin.AllBusinesses ? null : visible.Select(item => item.Code).ToArray();
        await using var db = await logs.CreateDbContextAsync();
        IQueryable<RenderLogRecord> Calls(DateTime from, DateTime to)
        {
            var query = db.RenderLogs.AsNoTracking().Where(log => log.CreatedAt >= from && log.CreatedAt < to);
            return codes is null ? query : query.Where(log => codes.Contains(log.TemplateCode));
        }

        // 按服务器所在时区划分“今天”和每一天。
        var localToday = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.Local).Date;
        DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);
        var days = new List<object>();
        for (var offset = 13; offset >= 0; offset--)
        {
            var day = localToday.AddDays(-offset);
            var calls = Calls(Utc(day), Utc(day.AddDays(1)));
            days.Add(new
            {
                date = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                total = await calls.CountAsync(),
                errors = await calls.CountAsync(log => log.Status == "error")
            });
        }

        var today = Calls(Utc(localToday), Utc(localToday.AddDays(1)));

        // 今日单张接口成功调用的 P95（最近秩法）：和设计书的 100 ms 目标对照。
        var single = today.Where(log => log.Status == "ok" && log.Endpoint == "render");
        var singleCount = await single.CountAsync();
        var p95 = singleCount == 0
            ? null
            : await single.OrderBy(log => log.DurationMs).Select(log => (double?)log.DurationMs).Skip((int)Math.Ceiling(singleCount * 0.95) - 1).FirstOrDefaultAsync();
        var todayStats = new
        {
            total = await today.CountAsync(),
            errors = await today.CountAsync(log => log.Status == "error"),
            slow = await today.CountAsync(log => log.IsSlow),
            labels = await today.SumAsync(log => (long?)log.ItemCount) ?? 0,
            averageMs = await today.Where(log => log.Status == "ok").AverageAsync(log => (double?)log.DurationMs) ?? 0,
            p95Ms = p95,
        };

        // 近 7 天单张接口最慢的模板：按平均耗时。
        var slowTemplates = await Calls(Utc(localToday.AddDays(-6)), Utc(localToday.AddDays(1)))
            .Where(log => log.Status == "ok" && log.Endpoint == "render")
            .GroupBy(log => log.TemplateCode)
            .Select(group => new { code = group.Key, calls = group.Count(), averageMs = group.Average(log => log.DurationMs), maxMs = group.Max(log => log.DurationMs) })
            .OrderByDescending(item => item.averageMs)
            .Take(5)
            .ToListAsync();
        var topTemplates = await Calls(Utc(localToday.AddDays(-6)), Utc(localToday.AddDays(1)))
            .GroupBy(log => log.TemplateCode)
            .Select(group => new { code = group.Key, calls = group.Count(), labels = group.Sum(log => log.ItemCount) })
            .OrderByDescending(item => item.calls)
            .Take(5)
            .ToListAsync();
        var recentOps = await db.OpLogs.AsNoTracking().OrderByDescending(log => log.Id).Take(8).ToListAsync();
        return Results.Json(new
        {
            templates = templateStats,
            today = todayStats,
            days,
            topTemplates,
            slowTemplates,
            recentOps
        });
    }
}
