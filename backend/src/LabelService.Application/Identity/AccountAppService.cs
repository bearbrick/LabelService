using LabelService.Application.Identity.Dtos;
using LabelService.Application.Logs;
using LabelService.Application.Shared;
using LabelService.Domain.Identity;
using LabelService.Domain.Shared;

namespace LabelService.Application.Identity;

/// <summary>
/// 账号的应用服务实现。
/// </summary>
/// <param name="users">用户仓储。</param>
/// <param name="roles">角色仓储。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="passwords">密码哈希。</param>
/// <param name="tokens">登录令牌。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="currentAdmin">当前用户。</param>
/// <param name="clock">时间来源。</param>
public sealed class AccountAppService(
    IUserRepository users,
    IRoleRepository roles,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwords,
    IAdminTokenService tokens,
    IOperationLogger operationLog,
    ICurrentAdmin currentAdmin,
    TimeProvider clock) : IAccountAppService
{
    /// <inheritdoc />
    public async Task<LoginResultDto> LoginAsync(LoginInput input)
    {
        var account = (input.Account ?? "").Trim().ToLowerInvariant();
        if (account.Length == 0 || string.IsNullOrEmpty(input.Password))
        {
            throw new UserFriendlyException("请输入账号和密码");
        }

        var user = await users.FindByAccountAsync(account);
        var now = clock.GetUtcNow().UtcDateTime;
        if (user is { LockedUntil: { } locked } && locked > now)
        {
            throw new UserFriendlyException(
                $"连续输错 {PasswordPolicy.MaxFailedLogins} 次，请 {Math.Ceiling((locked - now).TotalMinutes)} 分钟后再试，或请管理员重置密码",
                code: AdminErrors.Locked);
        }

        // 账号不存在时也算一次哈希，响应时间不暴露账号是否存在。
        var valid = passwords.Verify(input.Password, user?.PasswordHash ?? passwords.DummyHash);
        if (user is null || !valid)
        {
            if (user is not null)
            {
                // 连续输错到上限就锁定，并把计数清零，锁定期过后重新计。
                user.FailedLogins++;
                if (user.FailedLogins >= PasswordPolicy.MaxFailedLogins)
                {
                    user.LockedUntil = now.Add(PasswordPolicy.LockDuration);
                    user.FailedLogins = 0;
                }

                await unitOfWork.SaveChangesAsync();
            }

            await operationLog.WriteAsync(user?.Id, user?.Name ?? account, "登录", "系统", "登录失败：账号或密码不对");
            throw new UserFriendlyException("账号或密码不对", code: AdminErrors.InvalidLogin);
        }

        if (user.Status != UserStatuses.Active)
        {
            throw new UserFriendlyException("账号已停用，请联系管理员", code: AdminErrors.Disabled);
        }

        user.FailedLogins = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        await unitOfWork.SaveChangesAsync();
        var (token, expires) = tokens.Issue(user, input.Remember);
        await operationLog.WriteAsync(user.Id, user.Name, "登录", "系统", "登录成功");
        var role = await roles.FindAsync(user.RoleId) ?? throw new InvalidOperationException("用户的角色不存在");
        return new LoginResultDto(token, expires, ToCurrentUser(new AdminPrincipal(user, role)));
    }

    /// <inheritdoc />
    public Task LogoutAsync() => operationLog.WriteAsync("退出", "系统", "退出登录");

    /// <inheritdoc />
    public CurrentUserDto GetCurrentUser() => ToCurrentUser(currentAdmin.Principal);

    /// <inheritdoc />
    public async Task UpdateProfileAsync(UpdateProfileInput input)
    {
        var name = (input.Name ?? "").Trim();
        var phone = (input.Phone ?? "").Trim();
        if (name.Length is 0 or > 64 || phone.Length > 32)
        {
            throw new UserFriendlyException("姓名不能为空（最长 64 字），手机号最长 32 位");
        }

        var user = await users.FindAsync(currentAdmin.Principal.User.Id) ?? throw new InvalidOperationException("当前用户不存在");
        user.Name = name;
        user.Phone = phone;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("个人设置", user.Account, "修改了姓名或手机号");
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(ChangePasswordInput input)
    {
        var user = await users.FindAsync(currentAdmin.Principal.User.Id) ?? throw new InvalidOperationException("当前用户不存在");
        if (!passwords.Verify(input.OldPassword ?? "", user.PasswordHash))
        {
            throw new UserFriendlyException("原密码不对");
        }

        var newPassword = input.NewPassword ?? "";
        if (PasswordPolicy.Validate(newPassword) is { } problem)
        {
            throw new UserFriendlyException(problem);
        }

        if (newPassword == input.OldPassword)
        {
            throw new UserFriendlyException("新密码不能和原密码一样");
        }

        user.PasswordHash = passwords.Hash(newPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("个人设置", user.Account, "修改了密码");
    }

    /// <inheritdoc />
    public IReadOnlyList<PermissionDto> ListPermissions() =>
        PermissionNames.All.Select(item => new PermissionDto(item.Key, item.Label, item.Group)).ToList();

    /// <summary>
    /// 当前用户的信息。
    /// </summary>
    /// <param name="principal">登录的用户。</param>
    /// <returns>前端用的当前用户信息。</returns>
    private static CurrentUserDto ToCurrentUser(AdminPrincipal principal) => new(
        principal.User.Id,
        principal.User.Account,
        principal.User.Name,
        principal.User.Phone,
        new RoleBriefDto(principal.Role.Id, principal.Role.Name),
        PermissionNames.All.Select(item => item.Key).Where(principal.Can).ToArray(),
        principal.AllBusinesses,
        principal.Scope.Businesses.Order().ToArray(),
        principal.User.MustChangePassword,
        principal.User.LastLoginAt);
}
