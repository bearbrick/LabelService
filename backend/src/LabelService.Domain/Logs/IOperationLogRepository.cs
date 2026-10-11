namespace LabelService.Domain.Logs;

/// <summary>
/// 后台操作日志的仓储（日志库的 op_log 表）。每个方法独立执行、立即生效，不经过工作单元。
/// </summary>
public interface IOperationLogRepository
{
    /// <summary>
    /// 写一条操作日志。
    /// </summary>
    /// <param name="entity">操作日志。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写完时完成。</returns>
    Task AddAsync(OperationLogEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询一页操作日志，新的在前（翻页用上一页最后一条的 ID）。
    /// </summary>
    /// <param name="filter">筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>一页日志。</returns>
    Task<IReadOnlyList<OperationLogEntity>> SearchAsync(OperationLogFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// 操作日志的筛选条件。为 null 的条件不筛。
/// </summary>
/// <param name="From">起始时间（UTC，含）。</param>
/// <param name="To">结束时间（UTC，不含）。</param>
/// <param name="UserId">操作人 ID。</param>
/// <param name="Type">操作类型。</param>
/// <param name="Search">在对象、说明、操作人里模糊查找。</param>
/// <param name="BeforeId">翻页：上一页最后一条的 ID。</param>
/// <param name="PageSize">每页条数，1–200。</param>
public sealed record OperationLogFilter(
    DateTime? From = null,
    DateTime? To = null,
    int? UserId = null,
    string? Type = null,
    string? Search = null,
    long? BeforeId = null,
    int PageSize = 50);
