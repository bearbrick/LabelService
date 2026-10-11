namespace LabelService.Domain.Logs;

/// <summary>
/// 调用日志的仓储（日志库的 render_log 表）。每个方法独立执行、立即生效，不经过工作单元（日志库和主库是分开的）。
/// </summary>
public interface IRenderLogRepository
{
    /// <summary>
    /// 批量写入（后台服务按批写，一批一个事务）。
    /// </summary>
    /// <param name="entities">一批调用日志。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写完时完成。</returns>
    Task AddRangeAsync(IReadOnlyCollection<RenderLogEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除某个时间之前的日志（保留期清理）。
    /// </summary>
    /// <param name="beforeUtc">这个时间（UTC）之前的删掉。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除的条数。</returns>
    Task<int> DeleteBeforeAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询一页日志，按时间倒序（翻页用上一页最后一条的时间和 ID）。
    /// </summary>
    /// <param name="filter">筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>一页日志。</returns>
    Task<IReadOnlyList<RenderLogEntity>> SearchAsync(RenderLogFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 一段时间里的调用次数。
    /// </summary>
    /// <param name="fromUtc">起始时间（UTC，含）。</param>
    /// <param name="toUtc">结束时间（UTC，不含）。</param>
    /// <param name="templateCodes">只算这些模板；null 表示不限。</param>
    /// <param name="status">只算这个结果（ok、error、aborted）；null 表示不限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>次数。</returns>
    Task<int> CountAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes, string? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 一段时间里的汇总：次数、失败、慢调用、张数、平均耗时、单张接口成功调用的 P95。
    /// </summary>
    /// <param name="fromUtc">起始时间（UTC，含）。</param>
    /// <param name="toUtc">结束时间（UTC，不含）。</param>
    /// <param name="templateCodes">只算这些模板；null 表示不限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>汇总。</returns>
    Task<RenderLogTotals> GetTotalsAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// 调用次数最多的模板。
    /// </summary>
    /// <param name="fromUtc">起始时间（UTC，含）。</param>
    /// <param name="toUtc">结束时间（UTC，不含）。</param>
    /// <param name="templateCodes">只算这些模板；null 表示不限。</param>
    /// <param name="take">取前几个。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按调用次数从多到少。</returns>
    Task<IReadOnlyList<TemplateCallCount>> TopTemplatesAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// 单张接口成功调用里平均耗时最高的模板。
    /// </summary>
    /// <param name="fromUtc">起始时间（UTC，含）。</param>
    /// <param name="toUtc">结束时间（UTC，不含）。</param>
    /// <param name="templateCodes">只算这些模板；null 表示不限。</param>
    /// <param name="take">取前几个。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按平均耗时从高到低。</returns>
    Task<IReadOnlyList<TemplateLatency>> SlowTemplatesAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? templateCodes, int take, CancellationToken cancellationToken = default);
}
