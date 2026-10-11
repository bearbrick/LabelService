namespace LabelService.Domain.Identity;

/// <summary>
/// 管理后台的权限点（ADR 0006，和原型一致）。角色是一组权限点；模板相关的操作还要看用户的业务范围。
/// </summary>
public static class PermissionNames
{
    /// <summary>
    /// 查看模板。
    /// </summary>
    public const string TemplateView = "tpl.view";

    /// <summary>
    /// 新建和编辑模板。
    /// </summary>
    public const string TemplateEdit = "tpl.edit";

    /// <summary>
    /// 发布模板。
    /// </summary>
    public const string TemplatePublish = "tpl.publish";

    /// <summary>
    /// 删除模板。
    /// </summary>
    public const string TemplateDelete = "tpl.delete";

    /// <summary>
    /// 管理业务分组。
    /// </summary>
    public const string BusinessManage = "biz.manage";

    /// <summary>
    /// 管理画布预设、素材和拼版版式。
    /// </summary>
    public const string ResourceManage = "res.manage";

    /// <summary>
    /// 接口调试。
    /// </summary>
    public const string ApiTest = "api.test";

    /// <summary>
    /// 管理调用方和密钥。
    /// </summary>
    public const string ApiManage = "api.manage";

    /// <summary>
    /// 查看调用日志和操作日志。
    /// </summary>
    public const string LogView = "log.view";

    /// <summary>
    /// 管理用户。
    /// </summary>
    public const string UserManage = "sys.user";

    /// <summary>
    /// 管理角色和权限。
    /// </summary>
    public const string RoleManage = "sys.role";

    /// <summary>
    /// 全部权限点和说明，按分组排列；前端的角色编辑页按这个顺序显示。
    /// </summary>
    public static IReadOnlyList<PermissionDefinition> All
    {
        get;
    } =
    [
        new(TemplateView, "查看模板", "模板"),
        new(TemplateEdit, "新建和编辑模板", "模板"),
        new(TemplatePublish, "发布模板", "模板"),
        new(TemplateDelete, "删除模板", "模板"),
        new(BusinessManage, "管理业务分组", "模板"),
        new(ResourceManage, "管理画布预设和素材", "资源"),
        new(ApiTest, "接口调试", "接口"),
        new(ApiManage, "管理调用方和密钥", "接口"),
        new(LogView, "查看日志", "日志"),
        new(UserManage, "管理用户", "系统"),
        new(RoleManage, "管理角色权限", "系统"),
    ];

    /// <summary>
    /// 是不是认识的权限点。
    /// </summary>
    /// <param name="permission">权限点，如 tpl.view。</param>
    /// <returns>在 <see cref="All"/> 里时为 true。</returns>
    public static bool IsKnown(string permission) => All.Any(item => item.Key == permission);
}

/// <summary>
/// 一个权限点的定义。
/// </summary>
/// <param name="Key">权限点，如 tpl.view，存在角色的 permissions 列里。</param>
/// <param name="Label">中文名称，角色编辑页显示。</param>
/// <param name="Group">分组，如“模板”“系统”。</param>
public sealed record PermissionDefinition(string Key, string Label, string Group);
