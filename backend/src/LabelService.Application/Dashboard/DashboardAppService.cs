using System.Globalization;
using LabelService.Application.Dashboard.Dtos;
using LabelService.Application.Identity;
using LabelService.Application.Logs;
using LabelService.Domain.Identity;
using LabelService.Domain.Logs;
using LabelService.Domain.Templates;

namespace LabelService.Application.Dashboard;

/// <summary>
/// 概览页的应用服务实现。
/// </summary>
/// <param name="templates">模板仓储。</param>
/// <param name="renderLogs">调用日志仓储。</param>
/// <param name="operationLogs">操作日志仓储。</param>
/// <param name="currentAdmin">当前用户。</param>
/// <param name="clock">时间来源。</param>
public sealed class DashboardAppService(
    ITemplateRepository templates,
    IRenderLogRepository renderLogs,
    IOperationLogRepository operationLogs,
    ICurrentAdmin currentAdmin,
    TimeProvider clock) : IDashboardAppService
{
    /// <summary>
    /// 每日调用量统计的天数。
    /// </summary>
    private const int DayCount = 14;

    /// <summary>
    /// 最多、最慢模板的统计天数。
    /// </summary>
    private const int RankingDays = 7;

    /// <summary>
    /// 最多、最慢模板各列几个。
    /// </summary>
    private const int RankingSize = 5;

    /// <summary>
    /// 最近操作列几条。
    /// </summary>
    private const int RecentOpCount = 8;

    /// <inheritdoc />
    public async Task<DashboardDto> GetAsync()
    {
        var admin = currentAdmin.Principal;
        var visible = (await templates.ListAsync()).Where(item => admin.InScope(item.BusinessId)).ToList();
        var templateStats = new TemplateStatsDto(
            visible.Count,
            visible.Count(item => item.LatestVersion > 0),
            visible.Count(item => item.LatestVersion > 0 && item.HasDraft),
            visible.Count(item => item.LatestVersion == 0));
        if (!admin.Can(PermissionNames.LogView))
        {
            return new DashboardDto(templateStats);
        }

        // 业务范围不是全部时，调用统计只算自己范围里的模板。
        var codes = admin.AllBusinesses ? null : visible.Select(item => item.Code).ToArray();

        // 按服务器所在时区划分“今天”和每一天，查询时换算成 UTC。
        var localToday = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.Local).Date;
        var days = new List<DailyCallsDto>(DayCount);
        for (var offset = DayCount - 1; offset >= 0; offset--)
        {
            var day = localToday.AddDays(-offset);
            days.Add(new DailyCallsDto(
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                await renderLogs.CountAsync(ToUtc(day), ToUtc(day.AddDays(1)), codes),
                await renderLogs.CountAsync(ToUtc(day), ToUtc(day.AddDays(1)), codes, "error")));
        }

        var totals = await renderLogs.GetTotalsAsync(ToUtc(localToday), ToUtc(localToday.AddDays(1)), codes);
        var rankingFrom = ToUtc(localToday.AddDays(1 - RankingDays));
        var rankingTo = ToUtc(localToday.AddDays(1));
        var slow = await renderLogs.SlowTemplatesAsync(rankingFrom, rankingTo, codes, RankingSize);
        var top = await renderLogs.TopTemplatesAsync(rankingFrom, rankingTo, codes, RankingSize);
        var recent = await operationLogs.SearchAsync(new OperationLogFilter(PageSize: RecentOpCount));
        return new DashboardDto(
            templateStats,
            new CallStatsDto(totals.Total, totals.Errors, totals.Slow, totals.Labels, totals.AverageMs, totals.P95Ms),
            days,
            top.Select(item => new TemplateCallsDto(item.Code, item.Calls, item.Labels)).ToList(),
            slow.Select(item => new TemplateLatencyDto(item.Code, item.Calls, item.AverageMs, item.MaxMs)).ToList(),
            recent.Select(LogAppService.ToDto).ToList());
    }

    /// <summary>
    /// 服务器所在时区的时间换算成 UTC。
    /// </summary>
    /// <param name="local">本地时间（如某天零点）。</param>
    /// <returns>UTC 时间。</returns>
    private static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);
}
