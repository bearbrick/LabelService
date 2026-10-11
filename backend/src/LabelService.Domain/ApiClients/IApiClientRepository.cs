namespace LabelService.Domain.ApiClients;

/// <summary>
/// 调用方的仓储（表 api_client）。名字带 ForUpdate 的方法返回跟踪中的实体，改完调用工作单元保存。
/// </summary>
public interface IApiClientRepository
{
    /// <summary>
    /// 全部调用方，按名称排序，不跟踪。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>调用方列表。</returns>
    Task<IReadOnlyList<ApiClientEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 ID 取调用方，不跟踪。
    /// </summary>
    /// <param name="id">调用方 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>调用方；不存在时为 null。</returns>
    Task<ApiClientEntity?> FindAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 ID 取调用方，跟踪。
    /// </summary>
    /// <param name="id">调用方 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>调用方；不存在时为 null。</returns>
    Task<ApiClientEntity?> FindForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 业务范围不是“全部”、且选了分组的调用方，跟踪（删除业务分组时要去掉这个分组）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>调用方列表。</returns>
    Task<IReadOnlyList<ApiClientEntity>> ListWithBusinessScopeForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 名称是否已被别的调用方使用。
    /// </summary>
    /// <param name="name">名称。</param>
    /// <param name="exceptId">排除的调用方（修改时是它自己）；新建时传 0。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已被使用时为 true。</returns>
    Task<bool> NameExistsAsync(string name, int exceptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AppId 是否已被别的调用方使用。
    /// </summary>
    /// <param name="appId">AppId，非空。</param>
    /// <param name="exceptId">排除的调用方；新建时传 0。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已被使用时为 true。</returns>
    Task<bool> AppIdExistsAsync(string appId, int exceptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增调用方。
    /// </summary>
    /// <param name="client">新调用方。</param>
    void Add(ApiClientEntity client);
}
