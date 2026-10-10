using LabelService.Application.Identity.Dtos;
using LabelService.Application.Logs;
using LabelService.Application.Shared;
using LabelService.Domain.Identity;
using LabelService.Domain.Shared;

namespace LabelService.Application.Identity;

/// <summary>
/// 角色权限的应用服务实现。
/// </summary>
/// <param name="roles">角色仓储。</param>
/// <param name="users">用户仓储（统计每个角色的用户数）。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="clock">时间来源。</param>
public sealed class RoleAppService(
    IRoleRepository roles,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IOperationLogger operationLog,
    TimeProvider clock) : IRoleAppService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var list = await roles.ListAsync(cancellationToken);
        var usage = await users.CountByRoleAsync(cancellationToken);
        return list.Select(role => ToDto(role, usage.GetValueOrDefault(role.Id))).ToList();
    }

    /// <inheritdoc />
    public async Task<RoleDto> CreateAsync(CreateRoleDto input)
    {
        var role = new RoleEntity { CreatedAt = clock.GetUtcNow().UtcDateTime };
        var permissions = input.Permissions ?? [];
        if (input.CopyFromRoleId is { } source)
        {
            // 从已有角色复制：复制系统管理员时展开成全部权限点；来源不存在时视为没有权限点。
            var copy = await roles.FindAsync(source);
            permissions = copy is null ? [] : PermissionsOf(copy).ToArray();
        }

        await ApplyAsync(role, input.Name, input.Description, permissions);
        roles.Add(role);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("角色", role.Name, "新建角色");
        return ToDto(role, 0);
    }

    /// <inheritdoc />
    public async Task<RoleDto> UpdateAsync(int id, UpdateRoleDto input)
    {
        var role = await roles.FindAsync(id) ?? throw AdminErrors.NotFoundError("角色不存在");
        if (role.AllPermissions)
        {
            throw new UserFriendlyException("系统管理员角色不能修改");
        }

        await ApplyAsync(role, input.Name, input.Description, input.Permissions ?? []);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("角色", role.Name, "修改了名称、说明或权限");
        var usage = await users.CountByRoleAsync();
        return ToDto(role, usage.GetValueOrDefault(id));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id)
    {
        var role = await roles.FindAsync(id) ?? throw AdminErrors.NotFoundError("角色不存在");
        if (role.Builtin)
        {
            throw new UserFriendlyException("内置角色不能删除");
        }

        var usage = await users.CountByRoleAsync();
        if (usage.GetValueOrDefault(id) > 0)
        {
            throw new UserFriendlyException("还有用户在用这个角色，先给他们换角色");
        }

        roles.Remove(role);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("角色", role.Name, "删除角色");
    }

    /// <summary>
    /// 角色的权限点：系统管理员展开成全部权限点，其他角色按库里存的列。
    /// </summary>
    /// <param name="role">角色。</param>
    /// <returns>权限点。</returns>
    private static IEnumerable<string> PermissionsOf(RoleEntity role) => role.AllPermissions
        ? PermissionNames.All.Select(item => item.Key)
        : role.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// 转成接口返回的角色。
    /// </summary>
    /// <param name="role">角色实体。</param>
    /// <param name="userCount">有几个用户在用。</param>
    /// <returns>角色 DTO。</returns>
    private static RoleDto ToDto(RoleEntity role, int userCount) =>
        new(role.Id, role.Name, role.Description, role.AllPermissions, PermissionsOf(role).ToArray(), role.Builtin, userCount);

    /// <summary>
    /// 校验并写入名称、说明和权限点（新建、修改共用）。
    /// </summary>
    /// <param name="role">要写入的角色。</param>
    /// <param name="rawName">请求里的名称。</param>
    /// <param name="description">请求里的说明。</param>
    /// <param name="permissions">权限点。</param>
    /// <returns>写入完成时完成。</returns>
    /// <exception cref="UserFriendlyException">名称为空、太长或重复，不认识的权限点。</exception>
    private async Task ApplyAsync(RoleEntity role, string? rawName, string? description, string[] permissions)
    {
        var name = (rawName ?? "").Trim();
        if (!AccountRules.IsValidName(name))
        {
            throw new UserFriendlyException("角色名称不能为空，最长 64 字");
        }

        if (await roles.NameExistsAsync(name, role.Id))
        {
            throw new UserFriendlyException($"角色 {name} 已存在");
        }

        var distinct = permissions.Distinct().ToList();
        if (distinct.FirstOrDefault(item => !PermissionNames.IsKnown(item)) is { } unknown)
        {
            throw new UserFriendlyException($"不认识的权限点 {unknown}");
        }

        role.Name = name;
        role.Description = (description ?? "").Trim();
        role.Permissions = string.Join(",", distinct);
    }
}
