using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Admin;

/// <summary>
/// 登录、退出、当前用户、个人设置（改姓名手机号、改密码）。
/// </summary>
internal static class AccountEndpoints
{
    /// <summary>
    /// 连续输错几次锁定。
    /// </summary>
    public const int MaxFailedLogins = 5;

    /// <summary>
    /// 锁定多久。
    /// </summary>
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/login", LoginAsync);
        api.MapPost("/logout", LogoutAsync).RequireAdmin();
        api.MapGet("/me", (HttpContext http) => Results.Json(Me(http.CurrentAdmin()))).RequireAdmin();
        api.MapPut("/me", UpdateProfileAsync).RequireAdmin();
        api.MapPost("/me/password", ChangePasswordAsync).RequireAdmin();
        api.MapGet("/permissions", () => Permissions.All.Select(item => new { key = item.Key, label = item.Label, group = item.Group })).RequireAdmin();
    }

    private static async Task<IResult> LoginAsync(LoginRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, AdminTokens tokens, OperationLog log, TimeProvider clock)
    {
        var account = (body.Account ?? "").Trim().ToLowerInvariant();
        if (account.Length == 0 || string.IsNullOrEmpty(body.Password))
        {
            return AdminResults.Invalid("请输入账号和密码");
        }

        await using var db = await contexts.CreateDbContextAsync();
        var user = await db.Users.SingleOrDefaultAsync(item => item.Account == account);
        var now = clock.GetUtcNow().UtcDateTime;
        if (user is { LockedUntil: { } locked } && locked > now)
        {
            return AdminResults.Error(StatusCodes.Status403Forbidden, "LOCKED", $"连续输错 {MaxFailedLogins} 次，请 {Math.Ceiling((locked - now).TotalMinutes)} 分钟后再试，或请管理员重置密码");
        }

        // 账号不存在时也算一次哈希，响应时间不暴露账号是否存在。
        var valid = PasswordHasher.Verify(body.Password, user?.PasswordHash ?? PasswordHasher.Dummy);
        if (user is null || !valid)
        {
            if (user is not null)
            {
                user.FailedLogins++;
                if (user.FailedLogins >= MaxFailedLogins)
                {
                    user.LockedUntil = now.Add(LockDuration);
                    user.FailedLogins = 0;
                }

                await db.SaveChangesAsync();
            }

            await log.WriteAsync(http, user?.Id, user?.Name ?? account, "登录", "系统", "登录失败：账号或密码不对");
            return AdminResults.Error(StatusCodes.Status401Unauthorized, "INVALID_LOGIN", "账号或密码不对");
        }

        if (user.Status != UserStatuses.Active)
        {
            return AdminResults.Error(StatusCodes.Status403Forbidden, "DISABLED", "账号已停用，请联系管理员");
        }

        user.FailedLogins = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        await db.SaveChangesAsync();
        var (token, expires) = tokens.Issue(user, body.Remember);
        await log.WriteAsync(http, user.Id, user.Name, "登录", "系统", "登录成功");
        var role = await db.Roles.FindAsync(user.RoleId) ?? throw new InvalidOperationException("用户的角色不存在");
        return Results.Json(new
        {
            token,
            expiresAt = expires,
            me = Me(new AdminUser(user, role))
        });
    }

    private static async Task<IResult> LogoutAsync(HttpContext http, OperationLog log)
    {
        await log.WriteAsync(http, "退出", "系统", "退出登录");
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateProfileAsync(ProfileRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log, TimeProvider clock)
    {
        var name = (body.Name ?? "").Trim();
        var phone = (body.Phone ?? "").Trim();
        if (name.Length is 0 or > 64 || phone.Length > 32)
        {
            return AdminResults.Invalid("姓名不能为空（最长 64 字），手机号最长 32 位");
        }

        await using var db = await contexts.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(item => item.Id == http.CurrentAdmin().User.Id);
        user.Name = name;
        user.Phone = phone;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "个人设置", user.Account, "修改了姓名或手机号");
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePasswordAsync(PasswordRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log, TimeProvider clock)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(item => item.Id == http.CurrentAdmin().User.Id);
        if (!PasswordHasher.Verify(body.OldPassword ?? "", user.PasswordHash))
        {
            return AdminResults.Invalid("原密码不对");
        }

        var newPassword = body.NewPassword ?? "";
        if (PasswordHasher.Validate(newPassword) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        if (newPassword == body.OldPassword)
        {
            return AdminResults.Invalid("新密码不能和原密码一样");
        }

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "个人设置", user.Account, "修改了密码");
        return Results.NoContent();
    }

    /// <summary>
    /// 当前用户的信息，前端据此显示菜单、按钮。
    /// </summary>
    public static object Me(AdminUser admin) => new
    {
        id = admin.User.Id,
        account = admin.User.Account,
        name = admin.User.Name,
        phone = admin.User.Phone,
        role = new
        {
            id = admin.Role.Id,
            name = admin.Role.Name
        },
        permissions = Permissions.All.Select(item => item.Key).Where(admin.Can).ToArray(),
        allBusinesses = admin.AllBusinesses,
        businessIds = admin.Businesses.Order().ToArray(),
        mustChangePassword = admin.User.MustChangePassword,
        lastLoginAt = admin.User.LastLoginAt,
    };

    /// <summary>登录请求。</summary>
    internal sealed record LoginRequest(string? Account, string? Password, bool Remember);

    /// <summary>修改个人信息。</summary>
    internal sealed record ProfileRequest(string? Name, string? Phone);

    /// <summary>修改密码。</summary>
    internal sealed record PasswordRequest(string? OldPassword, string? NewPassword);
}
