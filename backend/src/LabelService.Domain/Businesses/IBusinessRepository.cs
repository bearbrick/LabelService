namespace LabelService.Domain.Businesses;

/// <summary>
/// 业务分组的仓储（表 business）。返回的实体会被跟踪，改完调用工作单元保存。
/// </summary>
public interface IBusinessRepository
{
    /// <summary>
    /// 全部业务分组，按排序号、ID 排序，不跟踪。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>业务分组列表。</returns>
    Task<IReadOnlyList<BusinessEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 ID 取业务分组。
    /// </summary>
    /// <param name="id">业务分组 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>业务分组；不存在时为 null。</returns>
    Task<BusinessEntity?> FindAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按编码取业务分组。
    /// </summary>
    /// <param name="code">大写编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>业务分组；不存在时为 null。</returns>
    Task<BusinessEntity?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 业务分组的个数（新建时排序号排在最后）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>个数。</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 编码是否已被别的业务分组使用。
    /// </summary>
    /// <param name="code">大写编码。</param>
    /// <param name="exceptId">排除的分组（修改时是它自己）；新建时传 0。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已被使用时为 true。</returns>
    Task<bool> CodeExistsAsync(string code, int exceptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 一组业务分组 ID 是否都存在。
    /// </summary>
    /// <param name="ids">业务分组 ID，不重复。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>都存在时为 true；空列表为 true。</returns>
    Task<bool> AllExistAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增业务分组。
    /// </summary>
    /// <param name="business">新分组。</param>
    void Add(BusinessEntity business);

    /// <summary>
    /// 删除业务分组。
    /// </summary>
    /// <param name="business">要删除的分组（跟踪中的实体）。</param>
    void Remove(BusinessEntity business);
}
