using System.Text;
using System.Text.Json;
using LabelService.Contracts.Protocol;

namespace LabelService.Backend.Checks;

/// <summary>
/// 管理后台的账号和权限（ADR 0006）：登录、首次改密码、锁定、令牌和 API Key 互不相通、用户和角色的规则、停用立即生效、操作日志。
/// </summary>
internal static class AdminAccountChecks
{
    /// <summary>
    /// 检查程序给 admin 改的新密码（只在临时数据目录里用）。
    /// </summary>
    public const string AdminPassword = "Admin2026x";

    /// <summary>
    /// 跑完返回管理员会话，给后面的检查用。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>已登录并改过密码的 admin 会话。</returns>
    public static async Task<AdminSession> RunAsync(HttpClient http, Action<string, bool> check)
    {
        var (login, first, me) = await AdminSession.LoginAsync(http, "admin", "admin123");
        check("首次启动建了 admin（开发环境初始密码 admin123），登录拿到令牌，必须先改密码",
            login.IsSuccessStatusCode && first is not null && me.GetProperty("me").GetProperty("mustChangePassword").GetBoolean()
            && me.GetProperty("me").GetProperty("permissions").GetArrayLength() == 11);
        var admin = first!;
        check("必须先改密码时，除个人设置外的接口返回 403 PASSWORD_CHANGE_REQUIRED",
            await AdminSession.CodeAsync(await admin.GetAsync("/api/admin/templates")) == "PASSWORD_CHANGE_REQUIRED" && (await admin.GetAsync("/api/admin/me")).IsSuccessStatusCode);
        check("改密码：原密码不对、新密码太弱都拒绝",
            await AdminSession.CodeAsync(await admin.PostAsync("/api/admin/me/password", new
            {
                oldPassword = "wrong",
                newPassword = AdminPassword
            })) == "INVALID"
            && await AdminSession.CodeAsync(await admin.PostAsync("/api/admin/me/password", new
            {
                oldPassword = "admin123",
                newPassword = "short"
            })) == "INVALID");
        var changed = await admin.PostAsync("/api/admin/me/password", new
        {
            oldPassword = "admin123",
            newPassword = AdminPassword
        });
        check("改密码后可以正常使用", changed.IsSuccessStatusCode && (await admin.GetAsync("/api/admin/templates")).IsSuccessStatusCode);
        var (oldLogin, _, _) = await AdminSession.LoginAsync(http, "admin", "admin123");
        check("旧密码不能再登录（401 INVALID_LOGIN）", (int)oldLogin.StatusCode == 401 && await AdminSession.CodeAsync(oldLogin) == "INVALID_LOGIN");
        var (_, relogin, _) = await AdminSession.LoginAsync(http, "admin", AdminPassword, remember: true);
        admin = relogin!;

        check("没有令牌、令牌乱写都返回 401",
            (int)(await new AdminSession(http, null).GetAsync("/api/admin/me")).StatusCode == 401
            && (int)(await new AdminSession(http, "not-a-token").GetAsync("/api/admin/me")).StatusCode == 401);
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/me"))
        {
            request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
            check("API Key 不能调管理接口（401）", (int)(await http.SendAsync(request)).StatusCode == 401);
        }

        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render") { Content = new StringContent($$"""{"templateCode":"BOX_LABEL","format":"png","data":{{Fixtures.BoxLabelData}}}""", Encoding.UTF8, "application/json") })
        {
            request.Headers.Authorization = new("Bearer", admin.Token);
            check("登录令牌不能调开放接口（401）", (int)(await http.SendAsync(request)).StatusCode == 401);
        }

        await CheckUsersAndRolesAsync(http, admin, check);
        return admin;
    }

    /// <summary>
    /// 用户和角色的规则：新建用户给临时密码、不能改自己的角色、不能停用自己、停用立即生效、内置角色不能删、在用的角色不能删。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="admin">已登录并改过密码的 admin 会话。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckUsersAndRolesAsync(HttpClient http, AdminSession admin, Action<string, bool> check)
    {
        var roles = await admin.JsonAsync(HttpMethod.Get, "/api/admin/roles");
        int Role(string name) => roles.EnumerateArray().Single(role => role.GetProperty("name").GetString() == name).GetProperty("id").GetInt32();
        check("内置 4 个角色：系统管理员、模板设计员、接口运维、只读访客", roles.GetArrayLength() == 4 && Role("模板设计员") > 0 && Role("只读访客") > 0);

        var created = await admin.JsonAsync(HttpMethod.Post, "/api/admin/users", new
        {
            account = "zhangming",
            name = "张明",
            roleId = Role("模板设计员"),
            allBusinesses = false,
            businessIds = Array.Empty<int>()
        });
        var temporary = created.GetProperty("temporaryPassword").GetString()!;
        var (_, designer, designerMe) = await AdminSession.LoginAsync(http, "zhangming", temporary);
        check("新建用户返回一次临时密码，用它登录后也必须先改密码",
            designer is not null && designerMe.GetProperty("me").GetProperty("mustChangePassword").GetBoolean() && designerMe.GetProperty("me").GetProperty("role").GetProperty("name").GetString() == "模板设计员");
        await designer!.PostAsync("/api/admin/me/password", new
        {
            oldPassword = temporary,
            newPassword = "Design2026x"
        });
        check("模板设计员没有 sys.user、biz.manage 权限（403 FORBIDDEN）",
            await AdminSession.CodeAsync(await designer.GetAsync("/api/admin/users")) == "FORBIDDEN"
            && await AdminSession.CodeAsync(await designer.PostAsync("/api/admin/businesses", new
            {
                code = "X",
                name = "X"
            })) == "FORBIDDEN");

        var adminId = (await admin.JsonAsync(HttpMethod.Get, "/api/admin/me")).GetProperty("id").GetInt32();
        check("不能修改自己的角色、不能停用自己",
            await AdminSession.CodeAsync(await admin.PutAsync($"/api/admin/users/{adminId}", new
            {
                name = "系统管理员",
                roleId = Role("只读访客"),
                allBusinesses = true
            })) == "INVALID"
            && await AdminSession.CodeAsync(await admin.PostAsync($"/api/admin/users/{adminId}/status", new
            {
                enabled = false
            })) == "INVALID");

        var designerId = created.GetProperty("user").GetProperty("id").GetInt32();
        await admin.PostAsync($"/api/admin/users/{designerId}/status", new
        {
            enabled = false
        });
        check("停用用户后，他已经拿到的令牌立即失效（401）", (int)(await designer.GetAsync("/api/admin/me")).StatusCode == 401);
        await admin.PostAsync($"/api/admin/users/{designerId}/status", new
        {
            enabled = true
        });

        for (var i = 0; i < 5; i++)
        {
            await AdminSession.LoginAsync(http, "zhangming", "wrong-password-" + i);
        }

        var (locked, _, _) = await AdminSession.LoginAsync(http, "zhangming", "Design2026x");
        check("连续输错 5 次后锁定，密码对也不能登录（LOCKED）", await AdminSession.CodeAsync(locked) == "LOCKED");
        var reset = await admin.JsonAsync(HttpMethod.Post, $"/api/admin/users/{designerId}/reset-password");
        var (unlocked, _, _) = await AdminSession.LoginAsync(http, "zhangming", reset.GetProperty("temporaryPassword").GetString()!);
        check("管理员重置密码后解锁，用新的临时密码能登录", unlocked.IsSuccessStatusCode);

        check("内置角色不能删除，系统管理员角色不能修改",
            await AdminSession.CodeAsync(await admin.DeleteAsync($"/api/admin/roles/{Role("只读访客")}")) == "INVALID"
            && await AdminSession.CodeAsync(await admin.PutAsync($"/api/admin/roles/{Role("系统管理员")}", new
            {
                name = "系统管理员",
                permissions = Array.Empty<string>()
            })) == "INVALID");
        var copied = await admin.JsonAsync(HttpMethod.Post, "/api/admin/roles", new
        {
            name = "包装设计员",
            description = "复制自模板设计员",
            copyFromRoleId = Role("模板设计员")
        });
        var copiedPermissions = copied.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).Order().ToArray();
        var designerPermissions = roles.EnumerateArray().Single(role => role.GetProperty("name").GetString() == "模板设计员").GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).Order().ToArray();
        check("新建角色可以复制已有角色的权限", copiedPermissions.SequenceEqual(designerPermissions));
        var copiedId = copied.GetProperty("id").GetInt32();
        var updated = await admin.JsonAsync(HttpMethod.Put, $"/api/admin/roles/{copiedId}", new
        {
            name = "包装设计员",
            permissions = new[] { "tpl.view", "tpl.edit" }
        });
        check("修改角色权限；不认识的权限点拒绝",
            updated.GetProperty("permissions").GetArrayLength() == 2
            && await AdminSession.CodeAsync(await admin.PutAsync($"/api/admin/roles/{copiedId}", new
            {
                name = "包装设计员",
                permissions = new[] { "tpl.fly" }
            })) == "INVALID");
        // 把张明换成这个自定义角色：有人在用时不能删；换回去以后可以删。
        await admin.PutAsync($"/api/admin/users/{designerId}", new
        {
            name = "张明",
            roleId = copiedId,
            allBusinesses = false,
            businessIds = Array.Empty<int>()
        });
        var inUse = await admin.DeleteAsync($"/api/admin/roles/{copiedId}");
        await admin.PutAsync($"/api/admin/users/{designerId}", new
        {
            name = "张明",
            roleId = Role("模板设计员"),
            allBusinesses = false,
            businessIds = Array.Empty<int>()
        });
        check("有人在用的自定义角色不能删除", await AdminSession.CodeAsync(inUse) == "INVALID");
        check("没人用的自定义角色可以删除", (await admin.DeleteAsync($"/api/admin/roles/{copiedId}")).IsSuccessStatusCode);

        var ops = await admin.JsonAsync(HttpMethod.Get, "/api/admin/op-logs?pageSize=200");
        var types = ops.EnumerateArray().Select(item => item.GetProperty("type").GetString()).ToHashSet();
        var loginFailures = await admin.JsonAsync(HttpMethod.Get, "/api/admin/op-logs?type=" + Uri.EscapeDataString("登录") + "&q=" + Uri.EscapeDataString("失败"));
        check("登录、改密码、用户、角色操作都记了操作日志，可以按类型和关键字筛选",
            types.IsSupersetOf(["登录", "个人设置", "用户", "角色"]) && loginFailures.GetArrayLength() >= 5
            && loginFailures.EnumerateArray().All(item => item.GetProperty("type").GetString() == "登录"));
    }

    /// <summary>
    /// 新建一个用户并改好密码，返回登录会话。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="admin">有用户管理权限的会话。</param>
    /// <param name="account">新账号。</param>
    /// <param name="roleName">角色名，如“设计员”。</param>
    /// <param name="allBusinesses">能访问全部业务分组。</param>
    /// <param name="businessIds">能访问的业务分组 ID。</param>
    /// <returns>新用户的会话（已改好密码）和用户 ID。</returns>
    public static async Task<(AdminSession Session, int Id)> CreateUserAsync(HttpClient http, AdminSession admin, string account, string roleName, bool allBusinesses, int[] businessIds)
    {
        var roles = await admin.JsonAsync(HttpMethod.Get, "/api/admin/roles");
        var roleId = roles.EnumerateArray().Single(role => role.GetProperty("name").GetString() == roleName).GetProperty("id").GetInt32();
        var created = await admin.JsonAsync(HttpMethod.Post, "/api/admin/users", new
        {
            account,
            name = account,
            roleId,
            allBusinesses,
            businessIds
        });
        var (_, session, _) = await AdminSession.LoginAsync(http, account, created.GetProperty("temporaryPassword").GetString()!);
        await session!.PostAsync("/api/admin/me/password", new
        {
            oldPassword = created.GetProperty("temporaryPassword").GetString(),
            newPassword = account + "2026x"
        });
        return (session, created.GetProperty("user").GetProperty("id").GetInt32());
    }
}
