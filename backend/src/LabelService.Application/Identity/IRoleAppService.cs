using LabelService.Application.Identity.Dtos;

namespace LabelService.Application.Identity;

/// <summary>
/// 角色权限（查看对所有登录用户开放，修改要权限点 sys.role）。
/// </summary>
/// <remarks>
/// 规则和原型一致：内置角色不能删除，系统管理员角色不能修改；有用户在用的角色不能删除。
/// </remarks>
public interface IRoleAppService
{
    /// <summary>
    /// 全部角色，按 ID 排序，带每个角色的用户数。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>角色列表。</returns>
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新建角色：直接给权限点，或者从已有角色复制。
    /// </summary>
    /// <param name="input">名称、说明、权限点或复制来源。</param>
    /// <returns>新角色。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">名称为空或重复、不认识的权限点。</exception>
    Task<RoleDto> CreateAsync(CreateRoleDto input);

    /// <summary>
    /// 修改名称、说明和权限点。
    /// </summary>
    /// <param name="id">角色 ID。</param>
    /// <param name="input">新的名称、说明和权限点。</param>
    /// <returns>修改后的角色。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">角色不存在（NOT_FOUND）、系统管理员角色、内容不合法。</exception>
    Task<RoleDto> UpdateAsync(int id, UpdateRoleDto input);

    /// <summary>
    /// 删除角色。
    /// </summary>
    /// <param name="id">角色 ID。</param>
    /// <returns>删除完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">角色不存在（NOT_FOUND）、内置角色、还有用户在用。</exception>
    Task DeleteAsync(int id);
}
