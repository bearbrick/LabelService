namespace LabelService.Application.Identity.Dtos;

/// <summary>
/// 登录请求。
/// </summary>
/// <param name="Account">账号（不区分大小写）。</param>
/// <param name="Password">密码。</param>
/// <param name="Remember">是否“7 天内自动登录”。</param>
public sealed record LoginInput(string? Account, string? Password, bool Remember);

/// <summary>
/// 登录结果。
/// </summary>
/// <param name="Token">登录令牌，后续请求放在 Authorization: Bearer 头里。</param>
/// <param name="ExpiresAt">令牌过期时间（UTC）。</param>
/// <param name="Me">当前用户的信息。</param>
public sealed record LoginResultDto(string Token, DateTime ExpiresAt, CurrentUserDto Me);

/// <summary>
/// 当前登录用户的信息，前端据此显示菜单和按钮。
/// </summary>
/// <param name="Id">用户 ID。</param>
/// <param name="Account">账号。</param>
/// <param name="Name">姓名。</param>
/// <param name="Phone">手机号。</param>
/// <param name="Role">角色。</param>
/// <param name="Permissions">拥有的权限点，按权限目录的顺序。</param>
/// <param name="AllBusinesses">能访问全部业务分组。</param>
/// <param name="BusinessIds">能访问的业务分组 ID，从小到大。</param>
/// <param name="MustChangePassword">是否必须先改密码。</param>
/// <param name="LastLoginAt">最后登录时间（UTC）。</param>
public sealed record CurrentUserDto(
    int Id,
    string Account,
    string Name,
    string Phone,
    RoleBriefDto Role,
    IReadOnlyList<string> Permissions,
    bool AllBusinesses,
    IReadOnlyList<int> BusinessIds,
    bool MustChangePassword,
    DateTime? LastLoginAt);

/// <summary>
/// 角色的简要信息。
/// </summary>
/// <param name="Id">角色 ID。</param>
/// <param name="Name">角色名。</param>
public sealed record RoleBriefDto(int Id, string Name);

/// <summary>
/// 修改个人信息。
/// </summary>
/// <param name="Name">姓名，不能为空，最长 64 字。</param>
/// <param name="Phone">手机号，可空，最长 32 位。</param>
public sealed record UpdateProfileInput(string? Name, string? Phone);

/// <summary>
/// 修改自己的密码。
/// </summary>
/// <param name="OldPassword">原密码。</param>
/// <param name="NewPassword">新密码，至少 8 位，同时有字母和数字。</param>
public sealed record ChangePasswordInput(string? OldPassword, string? NewPassword);

/// <summary>
/// 权限目录的一项（角色编辑页按这个顺序显示）。
/// </summary>
/// <param name="Key">权限点，如 tpl.view。</param>
/// <param name="Label">中文名称。</param>
/// <param name="Group">分组。</param>
public sealed record PermissionDto(string Key, string Label, string Group);
