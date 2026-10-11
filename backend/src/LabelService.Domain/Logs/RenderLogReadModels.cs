namespace LabelService.Domain.Logs;

/// <summary>
/// 调用日志的筛选条件。为 null 的条件不筛。
/// </summary>
/// <param name="From">起始时间（UTC，含）。</param>
/// <param name="To">结束时间（UTC，不含）。</param>
/// <param name="TemplateCode">模板编码。</param>
/// <param name="ClientId">调用方 ID。</param>
/// <param name="Status">结果：ok、error、aborted。</param>
/// <param name="Before">翻页：上一页最后一条的时间和 ID。</param>
/// <param name="PageSize">每页条数，最多 <see cref="MaxPageSize"/>。</param>
/// <param name="TemplateCodes">只看这些模板（按用户的业务范围限制）；null 表示不限。</param>
/// <param name="Search">按链路 ID、X-Request-Id、模板编码或错误码精确查找。</param>
public sealed record RenderLogFilter(
    DateTime? From = null,
    DateTime? To = null,
    string? TemplateCode = null,
    int? ClientId = null,
    string? Status = null,
    (DateTime CreatedAt, long Id)? Before = null,
    int PageSize = 50,
    IReadOnlyCollection<string>? TemplateCodes = null,
    string? Search = null)
{
    /// <summary>
    /// 每页最多条数。
    /// </summary>
    public const int MaxPageSize = 200;
}

/// <summary>
/// 一段时间里调用日志的汇总。
/// </summary>
/// <param name="Total">调用次数。</param>
/// <param name="Errors">失败次数。</param>
/// <param name="Slow">慢调用次数。</param>
/// <param name="Labels">生成张数（含份数）。</param>
/// <param name="AverageMs">成功调用的平均耗时，毫秒；没有成功调用时为 0。</param>
/// <param name="P95Ms">单张接口成功调用的 P95（最近秩法），毫秒；没有时为 null。</param>
public sealed record RenderLogTotals(int Total, int Errors, int Slow, long Labels, double AverageMs, double? P95Ms);

/// <summary>
/// 一个模板的调用次数和张数。
/// </summary>
/// <param name="Code">模板编码。</param>
/// <param name="Calls">调用次数。</param>
/// <param name="Labels">生成张数（含份数）。</param>
public sealed record TemplateCallCount(string Code, int Calls, int Labels);

/// <summary>
/// 一个模板的单张接口耗时。
/// </summary>
/// <param name="Code">模板编码。</param>
/// <param name="Calls">成功调用次数。</param>
/// <param name="AverageMs">平均耗时，毫秒。</param>
/// <param name="MaxMs">最长耗时，毫秒。</param>
public sealed record TemplateLatency(string Code, int Calls, double AverageMs, double MaxMs);
