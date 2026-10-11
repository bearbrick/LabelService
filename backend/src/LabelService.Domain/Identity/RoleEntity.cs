namespace LabelService.Domain.Identity;

/// <summary>
/// 表 app_role：角色，一组权限点（ADR 0006）。
/// </summary>
public sealed class RoleEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// 名称，全局唯一。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 说明。
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 是否拥有全部权限（系统管理员）。
    /// </summary>
    public bool AllPermissions
    {
        get; set;
    }

    /// <summary>
    /// 权限点，逗号分隔，取值见 <see cref="PermissionNames"/>。
    /// </summary>
    public string Permissions { get; set; } = "";

    /// <summary>
    /// 是否内置角色：内置角色不能删除，内置的系统管理员也不能改权限。
    /// </summary>
    public bool Builtin
    {
        get; set;
    }

    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}
