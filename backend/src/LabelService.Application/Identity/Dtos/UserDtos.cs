namespace LabelService.Application.Identity.Dtos;

/// <summary>
/// 后台用户（用户管理页的一行）。
/// </summary>
/// <param name="Id">用户 ID。</param>
/// <param name="Account">账号。</param>
/// <param name="Name">姓名。</param>
/// <param name="Phone">手机号。</param>
/// <param name="RoleId">角色 ID。</param>
/// <param name="AllBusinesses">能访问全部业务分组。</param>
/// <param name="BusinessIds">能访问的业务分组 ID，从小到大。</param>
/// <param name="Status">active 或 disabled。</param>
/// <param name="MustChangePassword">是否必须先改密码。</param>
/// <param name="LastLoginAt">最后登录时间（UTC）。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
public sealed record UserDto(
    int Id,
    string Account,
    string Name,
    string Phone,
    int RoleId,
    bool AllBusinesses,
    IReadOnlyList<int> BusinessIds,
    string Status,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    DateTime CreatedAt);

/// <summary>
/// 新建用户。
/// </summary>
/// <param name="Account">账号：小写字母开头，只能用小写字母、数字、点、下划线、连字符，3–32 位。</param>
/// <param name="Name">姓名。</param>
/// <param name="RoleId">角色 ID。</param>
/// <param name="AllBusinesses">能访问全部业务分组。</param>
/// <param name="BusinessIds">能访问的业务分组 ID。</param>
public sealed record CreateUserDto(string? Account, string? Name, int RoleId, bool AllBusinesses, int[]? BusinessIds);

/// <summary>
/// 修改用户（账号不能改）。
/// </summary>
/// <param name="Name">姓名。</param>
/// <param name="RoleId">角色 ID；不能改自己的角色。</param>
/// <param name="AllBusinesses">能访问全部业务分组。</param>
/// <param name="BusinessIds">能访问的业务分组 ID。</param>
public sealed record UpdateUserDto(string? Name, int RoleId, bool AllBusinesses, int[]? BusinessIds);

/// <summary>
/// 新建用户的结果：用户和临时密码（只返回这一次）。
/// </summary>
/// <param name="User">新用户。</param>
/// <param name="TemporaryPassword">临时密码，用户首次登录后必须改。</param>
public sealed record UserCreatedDto(UserDto User, string TemporaryPassword);

/// <summary>
/// 重置密码的结果。
/// </summary>
/// <param name="TemporaryPassword">新的临时密码（只返回这一次）。</param>
public sealed record PasswordResetDto(string TemporaryPassword);

/// <summary>
/// 启用或停用（用户、调用方共用）。
/// </summary>
/// <param name="Enabled">true 启用，false 停用。</param>
public sealed record SetStatusInput(bool Enabled);
