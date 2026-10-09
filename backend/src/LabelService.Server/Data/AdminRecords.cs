namespace LabelService.Server.Data;

/// <summary>
/// 表 business：业务分组。模板按业务分组归类，用户和调用方按业务分组授权（ADR 0006）。
/// </summary>
internal sealed class BusinessRecord
{
    /// <summary>主键。</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>编码，全局唯一，大写字母开头，如 PACK。</summary>
    public string Code { get; set; } = "";

    /// <summary>名称，如“成品包装”。</summary>
    public string Name { get; set; } = "";

    /// <summary>说明。</summary>
    public string Description { get; set; } = "";

    /// <summary>负责人（用户 ID）。</summary>
    public int? OwnerUserId
    {
        get; set;
    }

    /// <summary>标识色，#RRGGBB。</summary>
    public string Color { get; set; } = "#00939A";

    /// <summary>排序，小的在前。</summary>
    public int Sort
    {
        get; set;
    }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}

/// <summary>
/// 表 app_user：后台用户（内置账号，ADR 0006）。
/// </summary>
internal sealed class UserRecord
{
    /// <summary>主键。</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>登录账号，全局唯一，不区分大小写（存小写）。</summary>
    public string Account { get; set; } = "";

    /// <summary>姓名。</summary>
    public string Name { get; set; } = "";

    /// <summary>手机号（个人设置里填写，可空）。</summary>
    public string Phone { get; set; } = "";

    /// <summary>密码哈希，格式 pbkdf2-sha256$迭代次数$盐$哈希。</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>是否必须先改密码（首次登录、管理员重置密码后）。</summary>
    public bool MustChangePassword
    {
        get; set;
    }

    /// <summary>角色。</summary>
    public int RoleId
    {
        get; set;
    }

    /// <summary>是否能访问全部业务分组。</summary>
    public bool AllBusinesses
    {
        get; set;
    }

    /// <summary>能访问的业务分组 ID，逗号分隔；<see cref="AllBusinesses"/> 为 true 时忽略。</summary>
    public string BusinessIds { get; set; } = "";

    /// <summary>状态：active、disabled。</summary>
    public string Status { get; set; } = UserStatuses.Active;

    /// <summary>连续登录失败次数，登录成功后清零。</summary>
    public int FailedLogins
    {
        get; set;
    }

    /// <summary>锁定到什么时候（UTC），连续输错 5 次锁 15 分钟。</summary>
    public DateTime? LockedUntil
    {
        get; set;
    }

    /// <summary>最后登录时间（UTC）。</summary>
    public DateTime? LastLoginAt
    {
        get; set;
    }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt
    {
        get; set;
    }

    /// <summary>最后修改时间（UTC）。</summary>
    public DateTime UpdatedAt
    {
        get; set;
    }
}

/// <summary>
/// 用户状态取值。
/// </summary>
internal static class UserStatuses
{
    /// <summary>正常。</summary>
    public const string Active = "active";

    /// <summary>已停用，不能登录，已签发的令牌立即失效。</summary>
    public const string Disabled = "disabled";
}

/// <summary>
/// 表 app_role：角色，一组权限点。
/// </summary>
internal sealed class RoleRecord
{
    /// <summary>主键。</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>名称，全局唯一。</summary>
    public string Name { get; set; } = "";

    /// <summary>说明。</summary>
    public string Description { get; set; } = "";

    /// <summary>是否拥有全部权限（系统管理员）。</summary>
    public bool AllPermissions
    {
        get; set;
    }

    /// <summary>权限点，逗号分隔，取值见 Admin/Permissions.cs。</summary>
    public string Permissions { get; set; } = "";

    /// <summary>内置角色不能删除；内置的系统管理员也不能改权限。</summary>
    public bool Builtin
    {
        get; set;
    }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}

/// <summary>
/// 表 op_log（日志库）：后台操作日志。
/// </summary>
internal sealed class OpLogRecord
{
    /// <summary>主键。</summary>
    public long Id
    {
        get; set;
    }

    /// <summary>操作人 ID；登录失败时可能为空。</summary>
    public int? UserId
    {
        get; set;
    }

    /// <summary>操作人姓名（或尝试登录的账号）。</summary>
    public string UserName { get; set; } = "";

    /// <summary>操作类型，如 登录、发布模板、调用方。</summary>
    public string Type { get; set; } = "";

    /// <summary>操作对象，如 “BOX_LABEL 成品箱标”。</summary>
    public string Target { get; set; } = "";

    /// <summary>说明。</summary>
    public string Detail { get; set; } = "";

    /// <summary>来源 IP。</summary>
    public string Ip { get; set; } = "";

    /// <summary>时间（UTC）。</summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}
