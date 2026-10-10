using LabelService.Application.Identity;
using LabelService.Domain.Identity;
using LabelService.Domain.Shared;
using LabelService.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure.Hosting;

/// <summary>
/// 首次启动的账号初始化（ADR 0006）：建内置角色；库里没有用户时建 admin（系统管理员），首次登录后必须改密码。
/// </summary>
internal static class IdentitySeeder
{
    /// <summary>
    /// 首次启动建的管理员账号。
    /// </summary>
    public const string AdminAccount = "admin";

    /// <summary>
    /// 开发环境 admin 的初始密码（AGENTS.md 已公开，只在开发环境用）。
    /// </summary>
    private const string DevelopmentPassword = "admin123";

    /// <summary>
    /// 建内置角色和 admin 账号（已有的不动）。
    /// </summary>
    /// <param name="services">一个作用域里的服务。</param>
    /// <param name="environment">运行环境：开发环境用固定的初始密码。</param>
    /// <param name="logger">日志。</param>
    /// <returns>完成时完成。</returns>
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment, ILogger logger)
    {
        var roles = services.GetRequiredService<IRoleRepository>();
        var users = services.GetRequiredService<IUserRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var now = DateTime.UtcNow;
        if (!await roles.AnyAsync())
        {
            foreach (var (name, description, all, permissions) in BuiltinRoles.All)
            {
                roles.Add(new RoleEntity { Name = name, Description = description, AllPermissions = all, Permissions = string.Join(",", permissions), Builtin = true, CreatedAt = now });
            }

            await unitOfWork.SaveChangesAsync();
        }

        if (await users.AnyAsync())
        {
            return;
        }

        // 初始密码：配置了就用配置的；开发环境用 admin123；其他环境随机生成，写一条警告日志。
        var options = services.GetRequiredService<IOptions<AdminOptions>>().Value;
        var passwords = services.GetRequiredService<IPasswordHasher>();
        var password = !string.IsNullOrEmpty(options.InitialPassword) ? options.InitialPassword
            : environment.IsDevelopment() ? DevelopmentPassword : passwords.GenerateTemporary();
        var administrator = await roles.GetAdministratorAsync();
        users.Add(new UserEntity
        {
            Account = AdminAccount,
            Name = "系统管理员",
            PasswordHash = passwords.Hash(password),
            MustChangePassword = true,
            RoleId = administrator.Id,
            AllBusinesses = true,
            Status = UserStatuses.Active,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await unitOfWork.SaveChangesAsync();
        if (string.IsNullOrEmpty(options.InitialPassword) && !environment.IsDevelopment())
        {
            logger.LogWarning("已创建管理员账号 admin，临时密码 {Password}，首次登录后必须修改。这条日志只出现一次。", password);
        }
        else
        {
            logger.LogInformation("已创建管理员账号 admin，首次登录后必须修改密码");
        }
    }
}
