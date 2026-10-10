using LabelService.Domain.Identity;

namespace LabelService.Application.Identity;

/// <summary>
/// 当前登录的后台用户：账号、角色、权限点和业务范围。接口层每次请求从库里读出来（停用、改角色立即生效）。
/// </summary>
public sealed class AdminPrincipal
{
    /// <summary>
    /// 由用户和角色创建。
    /// </summary>
    /// <param name="user">用户。</param>
    /// <param name="role">用户的角色。</param>
    public AdminPrincipal(UserEntity user, RoleEntity role)
    {
        User = user;
        Role = role;

        // 系统管理员拥有全部权限点，其他角色按 permissions 列。
        Permissions = role.AllPermissions
            ? PermissionNames.All.Select(item => item.Key).ToHashSet(StringComparer.Ordinal)
            : role.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        Scope = BusinessScope.From(user.AllBusinesses, user.BusinessIds);
    }

    /// <summary>
    /// 用户。
    /// </summary>
    public UserEntity User
    {
        get;
    }

    /// <summary>
    /// 角色。
    /// </summary>
    public RoleEntity Role
    {
        get;
    }

    /// <summary>
    /// 拥有的权限点。
    /// </summary>
    public IReadOnlySet<string> Permissions
    {
        get;
    }

    /// <summary>
    /// 业务范围。
    /// </summary>
    public BusinessScope Scope
    {
        get;
    }

    /// <summary>
    /// 能访问全部业务分组。
    /// </summary>
    public bool AllBusinesses => Scope.AllBusinesses;

    /// <summary>
    /// 操作日志、模板记录里写的操作人，如“张三（zhangsan）”。
    /// </summary>
    public string DisplayName => $"{User.Name}（{User.Account}）";

    /// <summary>
    /// 有没有某个权限点。
    /// </summary>
    /// <param name="permission">权限点，见 <see cref="PermissionNames"/>。</param>
    /// <returns>有时为 true。</returns>
    public bool Can(string permission) => Permissions.Contains(permission);

    /// <summary>
    /// 业务分组在不在自己的范围里。没有分组的模板只有全部范围的用户能看。
    /// </summary>
    /// <param name="businessId">业务分组 ID，可空。</param>
    /// <returns>在范围里时为 true。</returns>
    public bool InScope(int? businessId) => Scope.Contains(businessId);
}
