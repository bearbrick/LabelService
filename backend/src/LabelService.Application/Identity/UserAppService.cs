using LabelService.Application.Identity.Dtos;
using LabelService.Application.Logs;
using LabelService.Application.Shared;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Shared;

namespace LabelService.Application.Identity;

/// <summary>
/// 用户管理的应用服务实现。
/// </summary>
/// <param name="users">用户仓储。</param>
/// <param name="roles">角色仓储。</param>
/// <param name="businesses">业务分组仓储。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="passwords">密码哈希。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="currentAdmin">当前用户。</param>
/// <param name="clock">时间来源。</param>
public sealed class UserAppService(
    IUserRepository users,
    IRoleRepository roles,
    IBusinessRepository businesses,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwords,
    IOperationLogger operationLog,
    ICurrentAdmin currentAdmin,
    TimeProvider clock) : IUserAppService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default) =>
        (await users.ListAsync(cancellationToken)).Select(ToDto).ToList();

    /// <inheritdoc />
    public async Task<UserCreatedDto> CreateAsync(CreateUserDto input)
    {
        var account = (input.Account ?? "").Trim().ToLowerInvariant();
        if (!AccountRules.IsValidAccount(account))
        {
            throw new UserFriendlyException(AccountRules.AccountRule);
        }

        if (await users.AccountExistsAsync(account))
        {
            throw new UserFriendlyException($"账号 {account} 已存在");
        }

        var user = new UserEntity { Account = account, Status = UserStatuses.Active, MustChangePassword = true, CreatedAt = clock.GetUtcNow().UtcDateTime };
        await ApplyAsync(user, input.Name, input.RoleId, input.AllBusinesses, input.BusinessIds);

        // 临时密码只在这次响应里返回，库里只存哈希。
        var password = passwords.GenerateTemporary();
        user.PasswordHash = passwords.Hash(password);
        user.UpdatedAt = user.CreatedAt;
        users.Add(user);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("用户", Describe(user), "新建用户");
        return new UserCreatedDto(ToDto(user), password);
    }

    /// <inheritdoc />
    public async Task<UserDto> UpdateAsync(int id, UpdateUserDto input)
    {
        var user = await FindAsync(id);
        if (id == currentAdmin.Principal.User.Id && input.RoleId != user.RoleId)
        {
            throw new UserFriendlyException("不能修改自己的角色");
        }

        await ApplyAsync(user, input.Name, input.RoleId, input.AllBusinesses, input.BusinessIds);
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("用户", Describe(user), "修改了姓名、角色或业务范围");
        return ToDto(user);
    }

    /// <inheritdoc />
    public async Task<UserDto> SetStatusAsync(int id, SetStatusInput input)
    {
        if (id == currentAdmin.Principal.User.Id && !input.Enabled)
        {
            throw new UserFriendlyException("不能停用自己");
        }

        var user = await FindAsync(id);
        user.Status = input.Enabled ? UserStatuses.Active : UserStatuses.Disabled;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("用户", Describe(user), input.Enabled ? "启用" : "停用");
        return ToDto(user);
    }

    /// <inheritdoc />
    public async Task<PasswordResetDto> ResetPasswordAsync(int id)
    {
        var user = await FindAsync(id);
        var password = passwords.GenerateTemporary();
        user.PasswordHash = passwords.Hash(password);
        user.MustChangePassword = true;

        // 重置密码同时解除锁定，用户马上可以用临时密码登录。
        user.FailedLogins = 0;
        user.LockedUntil = null;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("用户", Describe(user), "重置密码");
        return new PasswordResetDto(password);
    }

    /// <summary>
    /// 转成接口返回的用户。
    /// </summary>
    /// <param name="user">用户实体。</param>
    /// <returns>用户 DTO，业务分组 ID 从小到大。</returns>
    internal static UserDto ToDto(UserEntity user) => new(
        user.Id,
        user.Account,
        user.Name,
        user.Phone,
        user.RoleId,
        user.AllBusinesses,
        IdList.Parse(user.BusinessIds).Order().ToArray(),
        user.Status,
        user.MustChangePassword,
        user.LastLoginAt,
        user.CreatedAt);

    /// <summary>
    /// 操作日志里写的操作对象，如“张三（zhangsan）”。
    /// </summary>
    /// <param name="user">用户。</param>
    /// <returns>姓名和账号。</returns>
    private static string Describe(UserEntity user) => $"{user.Name}（{user.Account}）";

    /// <summary>
    /// 按 ID 取用户（跟踪）。
    /// </summary>
    /// <param name="id">用户 ID。</param>
    /// <returns>用户。</returns>
    /// <exception cref="UserFriendlyException">用户不存在（NOT_FOUND）。</exception>
    private async Task<UserEntity> FindAsync(int id) =>
        await users.FindAsync(id) ?? throw AdminErrors.NotFoundError("用户不存在");

    /// <summary>
    /// 校验并写入姓名、角色和业务范围（新建、修改共用）。
    /// </summary>
    /// <param name="user">要写入的用户。</param>
    /// <param name="rawName">请求里的姓名。</param>
    /// <param name="roleId">角色 ID。</param>
    /// <param name="allBusinesses">能访问全部业务分组。</param>
    /// <param name="businessIds">能访问的业务分组 ID。</param>
    /// <returns>写入完成时完成。</returns>
    /// <exception cref="UserFriendlyException">姓名为空或太长、角色不存在、业务分组不存在。</exception>
    private async Task ApplyAsync(UserEntity user, string? rawName, int roleId, bool allBusinesses, int[]? businessIds)
    {
        var name = (rawName ?? "").Trim();
        if (!AccountRules.IsValidName(name))
        {
            throw new UserFriendlyException("姓名不能为空，最长 64 字");
        }

        if (!await roles.ExistsAsync(roleId))
        {
            throw new UserFriendlyException("角色不存在");
        }

        // 选了“全部业务”时不看具体分组，也不存。
        var selected = (businessIds ?? []).Distinct().ToList();
        if (!allBusinesses && !await businesses.AllExistAsync(selected))
        {
            throw new UserFriendlyException("选择的业务分组有的不存在");
        }

        user.Name = name;
        user.RoleId = roleId;
        user.AllBusinesses = allBusinesses;
        user.BusinessIds = allBusinesses ? "" : IdList.Format(selected);
    }
}
