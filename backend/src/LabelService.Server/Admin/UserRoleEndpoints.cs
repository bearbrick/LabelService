using System.Text.RegularExpressions;
using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Admin;

/// <summary>
/// 用户管理（sys.user）和角色权限（sys.role）。
/// </summary>
/// <remarks>
/// 规则（和原型一致）：不能改自己的角色、不能停用自己；新建用户和重置密码返回一次临时密码，用户首次登录后必须改；
/// 内置角色不能删除，系统管理员角色不能改权限；有用户在用的角色不能删除。
/// </remarks>
internal static partial class UserRoleEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/users", ListUsersAsync).RequireAdmin(Permissions.UserManage);
        api.MapPost("/users", CreateUserAsync).RequireAdmin(Permissions.UserManage);
        api.MapPut("/users/{id:int}", UpdateUserAsync).RequireAdmin(Permissions.UserManage);
        api.MapPost("/users/{id:int}/status", SetUserStatusAsync).RequireAdmin(Permissions.UserManage);
        api.MapPost("/users/{id:int}/reset-password", ResetPasswordAsync).RequireAdmin(Permissions.UserManage);

        api.MapGet("/roles", ListRolesAsync).RequireAdmin();
        api.MapPost("/roles", CreateRoleAsync).RequireAdmin(Permissions.RoleManage);
        api.MapPut("/roles/{id:int}", UpdateRoleAsync).RequireAdmin(Permissions.RoleManage);
        api.MapDelete("/roles/{id:int}", DeleteRoleAsync).RequireAdmin(Permissions.RoleManage);
    }

    private static async Task<IResult> ListUsersAsync(IDbContextFactory<LabelDbContext> contexts)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking().OrderBy(user => user.Account).ToListAsync();
        return Results.Json(users.Select(View));
    }

    private static async Task<IResult> CreateUserAsync(UserRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log, TimeProvider clock)
    {
        var account = (body.Account ?? "").Trim().ToLowerInvariant();
        if (!AccountPattern().IsMatch(account))
        {
            return AdminResults.Invalid("账号只能用小写字母、数字、点、下划线、连字符，3–32 位，字母开头");
        }

        await using var db = await contexts.CreateDbContextAsync();
        if (await db.Users.AnyAsync(user => user.Account == account))
        {
            return AdminResults.Invalid($"账号 {account} 已存在");
        }

        var user = new UserRecord { Account = account, Status = UserStatuses.Active, MustChangePassword = true, CreatedAt = clock.GetUtcNow().UtcDateTime };
        if (await ApplyAsync(db, user, body) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        var password = PasswordHasher.Temporary();
        user.PasswordHash = PasswordHasher.Hash(password);
        user.UpdatedAt = user.CreatedAt;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "用户", $"{user.Name}（{user.Account}）", "新建用户");
        return Results.Json(new
        {
            user = View(user),
            temporaryPassword = password
        });
    }

    private static async Task<IResult> UpdateUserAsync(int id, UserRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log, TimeProvider clock)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return AdminResults.NotFound("用户不存在");
        }

        if (id == http.CurrentAdmin().User.Id && body.RoleId != user.RoleId)
        {
            return AdminResults.Invalid("不能修改自己的角色");
        }

        if (await ApplyAsync(db, user, body) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "用户", $"{user.Name}（{user.Account}）", "修改了姓名、角色或业务范围");
        return Results.Json(View(user));
    }

    private static async Task<IResult> SetUserStatusAsync(int id, StatusRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log, TimeProvider clock)
    {
        if (id == http.CurrentAdmin().User.Id && !body.Enabled)
        {
            return AdminResults.Invalid("不能停用自己");
        }

        await using var db = await contexts.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return AdminResults.NotFound("用户不存在");
        }

        user.Status = body.Enabled ? UserStatuses.Active : UserStatuses.Disabled;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "用户", $"{user.Name}（{user.Account}）", body.Enabled ? "启用" : "停用");
        return Results.Json(View(user));
    }

    private static async Task<IResult> ResetPasswordAsync(int id, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log, TimeProvider clock)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return AdminResults.NotFound("用户不存在");
        }

        var password = PasswordHasher.Temporary();
        user.PasswordHash = PasswordHasher.Hash(password);
        user.MustChangePassword = true;
        user.FailedLogins = 0;
        user.LockedUntil = null;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "用户", $"{user.Name}（{user.Account}）", "重置密码");
        return Results.Json(new
        {
            temporaryPassword = password
        });
    }

    private static async Task<IResult> ListRolesAsync(IDbContextFactory<LabelDbContext> contexts)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var roles = await db.Roles.AsNoTracking().OrderBy(role => role.Id).ToListAsync();
        var usage = await db.Users.GroupBy(user => user.RoleId).Select(group => new { group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.Key, item => item.Count);
        return Results.Json(roles.Select(role => View(role, usage.GetValueOrDefault(role.Id))));
    }

    private static async Task<IResult> CreateRoleAsync(RoleRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log, TimeProvider clock)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var role = new RoleRecord { CreatedAt = clock.GetUtcNow().UtcDateTime };
        var permissions = body.Permissions ?? [];
        if (body.CopyFromRoleId is { } source)
        {
            var copy = await db.Roles.FindAsync(source);
            permissions = copy is null ? [] : copy.AllPermissions ? Permissions.All.Select(item => item.Key).ToArray() : copy.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }

        if (await ApplyAsync(db, role, body with { Permissions = permissions }) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "角色", role.Name, "新建角色");
        return Results.Json(View(role, 0));
    }

    private static async Task<IResult> UpdateRoleAsync(int id, RoleRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var role = await db.Roles.FindAsync(id);
        if (role is null)
        {
            return AdminResults.NotFound("角色不存在");
        }

        if (role.AllPermissions)
        {
            return AdminResults.Invalid("系统管理员角色不能修改");
        }

        if (await ApplyAsync(db, role, body) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        await db.SaveChangesAsync();
        await log.WriteAsync(http, "角色", role.Name, "修改了名称、说明或权限");
        return Results.Json(View(role, await db.Users.CountAsync(user => user.RoleId == id)));
    }

    private static async Task<IResult> DeleteRoleAsync(int id, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var role = await db.Roles.FindAsync(id);
        if (role is null)
        {
            return AdminResults.NotFound("角色不存在");
        }

        if (role.Builtin)
        {
            return AdminResults.Invalid("内置角色不能删除");
        }

        if (await db.Users.AnyAsync(user => user.RoleId == id))
        {
            return AdminResults.Invalid("还有用户在用这个角色，先给他们换角色");
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "角色", role.Name, "删除角色");
        return Results.NoContent();
    }

    private static async Task<string?> ApplyAsync(LabelDbContext db, UserRecord user, UserRequest body)
    {
        var name = (body.Name ?? "").Trim();
        if (name.Length is 0 or > 64)
        {
            return "姓名不能为空，最长 64 字";
        }

        if (!await db.Roles.AnyAsync(role => role.Id == body.RoleId))
        {
            return "角色不存在";
        }

        var businesses = (body.BusinessIds ?? []).Distinct().ToList();
        if (!body.AllBusinesses && await db.Businesses.CountAsync(item => businesses.Contains(item.Id)) != businesses.Count)
        {
            return "选择的业务分组有的不存在";
        }

        user.Name = name;
        user.RoleId = body.RoleId;
        user.AllBusinesses = body.AllBusinesses;
        user.BusinessIds = body.AllBusinesses ? "" : string.Join(",", businesses);
        return null;
    }

    private static async Task<string?> ApplyAsync(LabelDbContext db, RoleRecord role, RoleRequest body)
    {
        var name = (body.Name ?? "").Trim();
        if (name.Length is 0 or > 64)
        {
            return "角色名称不能为空，最长 64 字";
        }

        if (await db.Roles.AnyAsync(item => item.Name == name && item.Id != role.Id))
        {
            return $"角色 {name} 已存在";
        }

        var permissions = (body.Permissions ?? []).Distinct().ToList();
        if (permissions.FirstOrDefault(item => !Permissions.IsKnown(item)) is { } unknown)
        {
            return $"不认识的权限点 {unknown}";
        }

        role.Name = name;
        role.Description = (body.Description ?? "").Trim();
        role.Permissions = string.Join(",", permissions);
        return null;
    }

    private static object View(UserRecord user) => new
    {
        id = user.Id,
        account = user.Account,
        name = user.Name,
        phone = user.Phone,
        roleId = user.RoleId,
        allBusinesses = user.AllBusinesses,
        businessIds = Auth.ApiKeyAuthenticator.ParseIds(user.BusinessIds).Order().ToArray(),
        status = user.Status,
        mustChangePassword = user.MustChangePassword,
        lastLoginAt = user.LastLoginAt,
        createdAt = user.CreatedAt,
    };

    private static object View(RoleRecord role, int users) => new
    {
        id = role.Id,
        name = role.Name,
        description = role.Description,
        allPermissions = role.AllPermissions,
        permissions = role.AllPermissions ? Permissions.All.Select(item => item.Key).ToArray() : role.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries),
        builtin = role.Builtin,
        users,
    };

    [GeneratedRegex("^[a-z][a-z0-9._-]{2,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountPattern();

    /// <summary>新建、修改用户。</summary>
    internal sealed record UserRequest(string? Account, string? Name, int RoleId, bool AllBusinesses, int[]? BusinessIds);

    /// <summary>启用、停用。</summary>
    internal sealed record StatusRequest(bool Enabled);

    /// <summary>新建、修改角色。新建时可以从已有角色复制权限。</summary>
    internal sealed record RoleRequest(string? Name, string? Description, string[]? Permissions, int? CopyFromRoleId = null);
}
