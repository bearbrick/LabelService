using LabelService.Server.Data;
using LabelService.Server.Templates;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Admin;

/// <summary>
/// 管理后台的注册和首次启动初始化（ADR 0006）。
/// </summary>
internal static class AdminSetup
{
    /// <summary>
    /// 注册 JWT 校验、令牌签发、操作日志。
    /// </summary>
    public static IServiceCollection AddAdmin(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AdminTokens>();
        services.AddSingleton<OperationLog>();
        services.AddAuthentication().AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<AdminTokens>((options, tokens) =>
        {
            // 令牌里的 sub 原样保留（不映射成 .NET 的长类型名），作为用户名。
            options.MapInboundClaims = false;
            options.TokenValidationParameters = tokens.ValidationParameters;
        });
        return services;
    }

    /// <summary>
    /// 首次启动：建内置角色；库里没有用户时建 admin（系统管理员），首次登录后必须改密码。主库的迁移由模板存储初始化执行。
    /// </summary>
    public static async Task InitializeAdminAsync(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<TemplateStoreOptions>>().Value.Source == TemplateSource.Files)
        {
            return;
        }

        // 读一次，确保签名密钥在启动时就准备好（没有配置时生成到数据目录）。
        app.Services.GetRequiredService<AdminTokens>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminSetup));
        await using var db = await app.Services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContextAsync();
        var now = DateTime.UtcNow;
        if (!await db.Roles.AnyAsync())
        {
            foreach (var (name, description, all, permissions) in Permissions.BuiltinRoles)
            {
                db.Roles.Add(new RoleRecord { Name = name, Description = description, AllPermissions = all, Permissions = string.Join(",", permissions), Builtin = true, CreatedAt = now });
            }

            await db.SaveChangesAsync();
        }

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var options = app.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
        var password = !string.IsNullOrEmpty(options.InitialPassword) ? options.InitialPassword
            : app.Environment.IsDevelopment() ? "admin123" : PasswordHasher.Temporary();
        var adminRole = await db.Roles.Where(role => role.AllPermissions).OrderBy(role => role.Id).FirstAsync();
        db.Users.Add(new UserRecord
        {
            Account = "admin",
            Name = "系统管理员",
            PasswordHash = PasswordHasher.Hash(password),
            MustChangePassword = true,
            RoleId = adminRole.Id,
            AllBusinesses = true,
            Status = UserStatuses.Active,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        if (string.IsNullOrEmpty(options.InitialPassword) && !app.Environment.IsDevelopment())
        {
            logger.LogWarning("已创建管理员账号 admin，临时密码 {Password}，首次登录后必须修改。这条日志只出现一次。", password);
        }
        else
        {
            logger.LogInformation("已创建管理员账号 admin，首次登录后必须修改密码");
        }
    }
}
