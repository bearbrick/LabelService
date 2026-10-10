namespace LabelService.Domain.Identity;

/// <summary>
/// 首次建库时创建的内置角色（和原型一致）。系统管理员拥有全部权限，不能修改和删除。
/// </summary>
public static class BuiltinRoles
{
    /// <summary>
    /// 系统管理员的角色名。首次启动创建的 admin 账号属于这个角色。
    /// </summary>
    public const string Administrator = "系统管理员";

    /// <summary>
    /// 全部内置角色。
    /// </summary>
    public static IReadOnlyList<BuiltinRoleDefinition> All
    {
        get;
    } =
    [
        new(Administrator, "全部权限", true, []),
        new("模板设计员", "设计和发布本业务的模板", false,
            [PermissionNames.TemplateView, PermissionNames.TemplateEdit, PermissionNames.TemplatePublish, PermissionNames.ResourceManage, PermissionNames.ApiTest, PermissionNames.LogView]),
        new("接口运维", "管理调用方，排查调用问题", false,
            [PermissionNames.TemplateView, PermissionNames.ApiTest, PermissionNames.ApiManage, PermissionNames.LogView]),
        new("只读访客", "只能查看模板和日志", false,
            [PermissionNames.TemplateView, PermissionNames.LogView]),
    ];
}

/// <summary>
/// 一个内置角色的定义。
/// </summary>
/// <param name="Name">角色名。</param>
/// <param name="Description">说明。</param>
/// <param name="AllPermissions">是否拥有全部权限。</param>
/// <param name="Permissions">权限点；<paramref name="AllPermissions"/> 为 true 时为空。</param>
public sealed record BuiltinRoleDefinition(string Name, string Description, bool AllPermissions, IReadOnlyList<string> Permissions);
