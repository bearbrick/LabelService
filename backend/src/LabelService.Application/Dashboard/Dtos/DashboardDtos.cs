using LabelService.Application.Logs.Dtos;

namespace LabelService.Application.Dashboard.Dtos;

/// <summary>
/// 概览页的数据。没有权限点 log.view 时只有模板数量，其余为 null（不出现在 JSON 里）。
/// </summary>
/// <param name="Templates">模板数量（按业务范围）。</param>
/// <param name="Today">今天的调用。</param>
/// <param name="Days">近 14 天每天的调用量和失败数，旧的在前。</param>
/// <param name="TopTemplates">近 7 天调用最多的模板（最多 5 个）。</param>
/// <param name="SlowTemplates">近 7 天单张接口平均耗时最长的模板（最多 5 个）。</param>
/// <param name="RecentOps">最近的 8 条操作。</param>
public sealed record DashboardDto(
    TemplateStatsDto Templates,
    CallStatsDto? Today = null,
    IReadOnlyList<DailyCallsDto>? Days = null,
    IReadOnlyList<TemplateCallsDto>? TopTemplates = null,
    IReadOnlyList<TemplateLatencyDto>? SlowTemplates = null,
    IReadOnlyList<OperationLogDto>? RecentOps = null);

/// <summary>
/// 模板数量。
/// </summary>
/// <param name="Total">全部模板。</param>
/// <param name="Published">发布过的。</param>
/// <param name="Changed">发布过、又有未发布草稿的。</param>
/// <param name="NeverPublished">从未发布的。</param>
public sealed record TemplateStatsDto(int Total, int Published, int Changed, int NeverPublished);

/// <summary>
/// 一段时间里的调用统计。
/// </summary>
/// <param name="Total">调用次数。</param>
/// <param name="Errors">失败次数。</param>
/// <param name="Slow">慢调用次数。</param>
/// <param name="Labels">生成的标签张数。</param>
/// <param name="AverageMs">成功调用的平均耗时，毫秒。</param>
/// <param name="P95Ms">单张接口成功调用的 P95，毫秒（和 100 ms 目标对照）；没有调用时为 null。</param>
public sealed record CallStatsDto(int Total, int Errors, int Slow, long Labels, double AverageMs, double? P95Ms);

/// <summary>
/// 一天的调用量。
/// </summary>
/// <param name="Date">日期（服务器所在时区），yyyy-MM-dd。</param>
/// <param name="Total">调用次数。</param>
/// <param name="Errors">失败次数。</param>
public sealed record DailyCallsDto(string Date, int Total, int Errors);

/// <summary>
/// 一个模板的调用量。
/// </summary>
/// <param name="Code">模板编码。</param>
/// <param name="Calls">调用次数。</param>
/// <param name="Labels">生成的标签张数。</param>
public sealed record TemplateCallsDto(string Code, int Calls, int Labels);

/// <summary>
/// 一个模板的耗时。
/// </summary>
/// <param name="Code">模板编码。</param>
/// <param name="Calls">成功调用次数。</param>
/// <param name="AverageMs">平均耗时，毫秒。</param>
/// <param name="MaxMs">最长耗时，毫秒。</param>
public sealed record TemplateLatencyDto(string Code, int Calls, double AverageMs, double MaxMs);
