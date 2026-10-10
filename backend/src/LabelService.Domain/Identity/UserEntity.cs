namespace LabelService.Domain.Identity;

/// <summary>
/// 表 app_user：管理后台的用户（内置账号，ADR 0006）。
/// </summary>
public sealed class UserEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// 登录账号，全局唯一，不区分大小写（存小写）。
    /// </summary>
    public string Account { get; set; } = "";

    /// <summary>
    /// 姓名。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 手机号（个人设置里填写，可空）。
    /// </summary>
    public string Phone { get; set; } = "";

    /// <summary>
    /// 密码哈希，格式 pbkdf2-sha256$迭代次数$盐$哈希。
    /// </summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>
    /// 是否必须先改密码（首次登录、管理员重置密码后）。
    /// </summary>
    public bool MustChangePassword
    {
        get; set;
    }

    /// <summary>
    /// 角色 ID。
    /// </summary>
    public int RoleId
    {
        get; set;
    }

    /// <summary>
    /// 是否能访问全部业务分组。
    /// </summary>
    public bool AllBusinesses
    {
        get; set;
    }

    /// <summary>
    /// 能访问的业务分组 ID，逗号分隔；<see cref="AllBusinesses"/> 为 true 时忽略。
    /// </summary>
    public string BusinessIds { get; set; } = "";

    /// <summary>
    /// 状态，取值见 <see cref="UserStatuses"/>。
    /// </summary>
    public string Status { get; set; } = UserStatuses.Active;

    /// <summary>
    /// 连续登录失败次数，登录成功后清零。
    /// </summary>
    public int FailedLogins
    {
        get; set;
    }

    /// <summary>
    /// 锁定到什么时候（UTC）；连续输错 5 次锁 15 分钟。
    /// </summary>
    public DateTime? LockedUntil
    {
        get; set;
    }

    /// <summary>
    /// 最后登录时间（UTC）。
    /// </summary>
    public DateTime? LastLoginAt
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

    /// <summary>
    /// 最后修改时间（UTC）。
    /// </summary>
    public DateTime UpdatedAt
    {
        get; set;
    }
}
