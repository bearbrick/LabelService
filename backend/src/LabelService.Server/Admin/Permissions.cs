namespace LabelService.Server.Admin;

/// <summary>
/// 后台权限点（ADR 0006，和原型一致）。角色是一组权限点；模板相关的操作还要看用户的业务范围。
/// </summary>
internal static class Permissions
{
    /// <summary>查看模板。</summary>
    public const string TemplateView = "tpl.view";

    /// <summary>新建和编辑模板。</summary>
    public const string TemplateEdit = "tpl.edit";

    /// <summary>发布模板。</summary>
    public const string TemplatePublish = "tpl.publish";

    /// <summary>删除模板。</summary>
    public const string TemplateDelete = "tpl.delete";

    /// <summary>管理业务分组。</summary>
    public const string BusinessManage = "biz.manage";

    /// <summary>管理画布预设和素材。</summary>
    public const string ResourceManage = "res.manage";

    /// <summary>接口调试。</summary>
    public const string ApiTest = "api.test";

    /// <summary>管理调用方和密钥。</summary>
    public const string ApiManage = "api.manage";

    /// <summary>查看日志。</summary>
    public const string LogView = "log.view";

    /// <summary>管理用户。</summary>
    public const string UserManage = "sys.user";

    /// <summary>管理角色权限。</summary>
    public const string RoleManage = "sys.role";

    /// <summary>
    /// 全部权限点和说明，按分组排列，前端的角色编辑页按这个顺序显示。
    /// </summary>
    public static IReadOnlyList<(string Key, string Label, string Group)> All
    {
        get;
    } =
    [
        (TemplateView, "查看模板", "模板"),
        (TemplateEdit, "新建和编辑模板", "模板"),
        (TemplatePublish, "发布模板", "模板"),
        (TemplateDelete, "删除模板", "模板"),
        (BusinessManage, "管理业务分组", "模板"),
        (ResourceManage, "管理画布预设和素材", "资源"),
        (ApiTest, "接口调试", "接口"),
        (ApiManage, "管理调用方和密钥", "接口"),
        (LogView, "查看日志", "日志"),
        (UserManage, "管理用户", "系统"),
        (RoleManage, "管理角色权限", "系统"),
    ];

    /// <summary>
    /// 是不是认识的权限点。
    /// </summary>
    public static bool IsKnown(string permission) => All.Any(item => item.Key == permission);

    /// <summary>
    /// 首次建库时创建的内置角色（和原型一致）。系统管理员拥有全部权限，不能修改和删除。
    /// </summary>
    public static IReadOnlyList<(string Name, string Description, bool All, string[] Permissions)> BuiltinRoles
    {
        get;
    } =
    [
        ("系统管理员", "全部权限", true, []),
        ("模板设计员", "设计和发布本业务的模板", false, [TemplateView, TemplateEdit, TemplatePublish, ResourceManage, ApiTest, LogView]),
        ("接口运维", "管理调用方，排查调用问题", false, [TemplateView, ApiTest, ApiManage, LogView]),
        ("只读访客", "只能查看模板和日志", false, [TemplateView, LogView]),
    ];
}
