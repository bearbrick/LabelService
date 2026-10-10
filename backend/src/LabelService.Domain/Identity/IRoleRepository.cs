namespace LabelService.Domain.Identity;

/// <summary>
/// 角色的仓储（表 app_role）。返回的实体会被跟踪，改完调用工作单元保存。
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    /// 按 ID 取角色。
    /// </summary>
    /// <param name="id">角色 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>角色；不存在时为 null。</returns>
    Task<RoleEntity?> FindAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全部角色，按 ID 排序，不跟踪。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>角色列表。</returns>
    Task<IReadOnlyList<RoleEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 库里有没有角色（首次启动时判断要不要建内置角色）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>有角色时为 true。</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 角色是否存在。
    /// </summary>
    /// <param name="id">角色 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存在时为 true。</returns>
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 名称是否已被别的角色使用。
    /// </summary>
    /// <param name="name">角色名。</param>
    /// <param name="exceptId">排除的角色（修改时是它自己）；新建时传 0。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已被使用时为 true。</returns>
    Task<bool> NameExistsAsync(string name, int exceptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 系统管理员角色（拥有全部权限、ID 最小的那个）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>系统管理员角色。</returns>
    Task<RoleEntity> GetAdministratorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增角色。
    /// </summary>
    /// <param name="role">新角色。</param>
    void Add(RoleEntity role);

    /// <summary>
    /// 删除角色。
    /// </summary>
    /// <param name="role">要删除的角色（跟踪中的实体）。</param>
    void Remove(RoleEntity role);
}
