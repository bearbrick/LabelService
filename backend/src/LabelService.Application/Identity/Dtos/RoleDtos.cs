namespace LabelService.Application.Identity.Dtos;

/// <summary>
/// 角色（角色权限页的一列）。
/// </summary>
/// <param name="Id">角色 ID。</param>
/// <param name="Name">角色名。</param>
/// <param name="Description">说明。</param>
/// <param name="AllPermissions">是否拥有全部权限（系统管理员）。</param>
/// <param name="Permissions">权限点；系统管理员列出全部。</param>
/// <param name="Builtin">是否内置角色。</param>
/// <param name="Users">有几个用户在用。</param>
public sealed record RoleDto(int Id, string Name, string Description, bool AllPermissions, IReadOnlyList<string> Permissions, bool Builtin, int Users);

/// <summary>
/// 新建角色：直接给权限点，或者从已有角色复制。
/// </summary>
/// <param name="Name">角色名，全局唯一。</param>
/// <param name="Description">说明。</param>
/// <param name="Permissions">权限点；给了 <paramref name="CopyFromRoleId"/> 时忽略。</param>
/// <param name="CopyFromRoleId">从这个角色复制权限点。</param>
public sealed record CreateRoleDto(string? Name, string? Description, string[]? Permissions, int? CopyFromRoleId = null);

/// <summary>
/// 修改角色（系统管理员角色不能改）。
/// </summary>
/// <param name="Name">角色名。</param>
/// <param name="Description">说明。</param>
/// <param name="Permissions">权限点。</param>
public sealed record UpdateRoleDto(string? Name, string? Description, string[]? Permissions);
