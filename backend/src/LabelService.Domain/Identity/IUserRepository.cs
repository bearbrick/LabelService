namespace LabelService.Domain.Identity;

/// <summary>
/// 后台用户的仓储（表 app_user）。返回的实体会被跟踪，改完调用工作单元保存。
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// 按 ID 取用户。
    /// </summary>
    /// <param name="id">用户 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户；不存在时为 null。</returns>
    Task<UserEntity?> FindAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按账号取用户（账号存小写）。
    /// </summary>
    /// <param name="account">小写账号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户；不存在时为 null。</returns>
    Task<UserEntity?> FindByAccountAsync(string account, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全部用户，按账号排序，不跟踪。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户列表。</returns>
    Task<IReadOnlyList<UserEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 业务范围不是“全部”、且选了分组的用户（删除业务分组时要从他们的范围里去掉这个分组）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户列表（跟踪）。</returns>
    Task<IReadOnlyList<UserEntity>> ListWithBusinessScopeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 库里有没有用户（首次启动时判断要不要建 admin）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>有用户时为 true。</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 用户是否存在。
    /// </summary>
    /// <param name="id">用户 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存在时为 true。</returns>
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 账号是否已被使用。
    /// </summary>
    /// <param name="account">小写账号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已被使用时为 true。</returns>
    Task<bool> AccountExistsAsync(string account, CancellationToken cancellationToken = default);

    /// <summary>
    /// 每个角色有几个用户。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>角色 ID → 人数；没人用的角色不在里面。</returns>
    Task<IReadOnlyDictionary<int, int>> CountByRoleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增用户（保存后才有 ID）。
    /// </summary>
    /// <param name="user">新用户。</param>
    void Add(UserEntity user);
}
