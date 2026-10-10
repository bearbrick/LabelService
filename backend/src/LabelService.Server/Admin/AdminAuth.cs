using System.Security.Claims;
using System.Security.Cryptography;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LabelService.Server.Admin;

/// <summary>
/// 管理后台的登录令牌（ADR 0006）：HS256 JWT，令牌里只放用户 ID，权限每次请求从库里读。
/// </summary>
internal sealed class AdminTokens
{
    /// <summary>
    /// 签发者和受众。
    /// </summary>
    public const string Issuer = "LabelService";

    private readonly SymmetricSecurityKey key;
    private readonly TimeProvider clock;

    /// <summary>
    /// 读取签名密钥：配置 LabelService:Admin:SigningKey；没有时用数据目录里的 admin-signing.key，没有就生成一个。
    /// </summary>
    public AdminTokens(IOptions<AdminOptions> options, IOptions<DataOptions> data, TimeProvider clock)
    {
        this.clock = clock;
        key = new SymmetricSecurityKey(ResolveKey(options.Value, data.Value));
    }

    /// <summary>
    /// 校验令牌用的参数。
    /// </summary>
    public TokenValidationParameters ValidationParameters => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Issuer,
        IssuerSigningKey = key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = JwtRegisteredClaimNames.Sub,
    };

    /// <summary>
    /// 签发令牌。勾选“7 天内自动登录”时 7 天，否则 8 小时。
    /// </summary>
    public (string Token, DateTime ExpiresAt) Issue(UserEntity user, bool remember)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.Add(remember ? TimeSpan.FromDays(7) : TimeSpan.FromHours(8));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Issuer,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))]),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    private static byte[] ResolveKey(AdminOptions options, DataOptions data)
    {
        if (!string.IsNullOrWhiteSpace(options.SigningKey))
        {
            var configured = Convert.FromBase64String(options.SigningKey);
            return configured.Length >= 32 ? configured : throw new InvalidOperationException("LabelService:Admin:SigningKey 至少 32 字节（Base64）");
        }

        var directory = data.ResolveDirectory();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "admin-signing.key");
        if (File.Exists(path))
        {
            return Convert.FromBase64String(File.ReadAllText(path).Trim());
        }

        var generated = RandomNumberGenerator.GetBytes(64);
        File.WriteAllText(path, Convert.ToBase64String(generated));
        return generated;
    }
}

/// <summary>
/// 配置节 LabelService:Admin。
/// </summary>
internal sealed class AdminOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Admin";

    /// <summary>
    /// JWT 签名密钥（Base64，至少 32 字节）。不配置时用数据目录里的 admin-signing.key。不要写进仓库。
    /// </summary>
    public string SigningKey { get; set; } = "";

    /// <summary>
    /// 首次启动时 admin 账号的密码。不配置时开发环境用 admin123，其他环境随机生成并写一条警告日志。
    /// </summary>
    public string InitialPassword { get; set; } = "";
}

/// <summary>
/// 当前登录的后台用户和权限，由 <see cref="AdminFilter"/> 每次请求从库里读出来放进 HttpContext。
/// </summary>
internal sealed class AdminUser(UserEntity user, RoleEntity role)
{
    private readonly IReadOnlySet<string> permissions = role.AllPermissions
        ? PermissionNames.All.Select(item => item.Key).ToHashSet(StringComparer.Ordinal)
        : role.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    private readonly IReadOnlySet<int> businesses = Auth.ApiKeyAuthenticator.ParseIds(user.BusinessIds);

    /// <summary>用户记录。</summary>
    public UserEntity User { get; } = user;

    /// <summary>角色。</summary>
    public RoleEntity Role { get; } = role;

    /// <summary>权限点。</summary>
    public IReadOnlySet<string> Permissions => permissions;

    /// <summary>能访问的业务分组；<see cref="AllBusinesses"/> 为 true 时不看。</summary>
    public IReadOnlySet<int> Businesses => businesses;

    /// <summary>能访问全部业务分组。</summary>
    public bool AllBusinesses => User.AllBusinesses;

    /// <summary>操作日志、记录里写的操作人。</summary>
    public string DisplayName => $"{User.Name}（{User.Account}）";

    /// <summary>有没有某个权限点。</summary>
    public bool Can(string permission) => permissions.Contains(permission);

    /// <summary>业务分组在不在自己的范围里。没有分组的模板只有全部范围的用户能看。</summary>
    public bool InScope(int? businessId) => AllBusinesses || (businessId is { } id && businesses.Contains(id));
}

/// <summary>
/// 管理接口的过滤器：要求登录，从库里读用户（停用、改角色立即生效），检查权限点；必须先改密码时只放行个人设置。
/// </summary>
internal sealed class AdminFilter(string? permission) : IEndpointFilter
{
    /// <summary>
    /// HttpContext.Items 里放当前用户的键。
    /// </summary>
    public const string ItemKey = "LabelService.AdminUser";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var result = await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!result.Succeeded || !int.TryParse(result.Principal?.Identity?.Name, out var userId))
        {
            return AdminResults.Error(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "请先登录");
        }

        var contexts = http.RequestServices.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<LabelDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(http.RequestAborted);
        var user = await db.Users.FindAsync([userId], http.RequestAborted);
        var role = user is null ? null : await db.Roles.FindAsync([user.RoleId], http.RequestAborted);
        if (user is not { Status: UserStatuses.Active } || role is null)
        {
            return AdminResults.Error(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "账号已停用或不存在，请重新登录");
        }

        var admin = new AdminUser(user, role);
        http.Items[ItemKey] = admin;
        if (user.MustChangePassword && permission is not null)
        {
            return AdminResults.Error(StatusCodes.Status403Forbidden, "PASSWORD_CHANGE_REQUIRED", "请先在个人设置里修改密码");
        }

        if (permission is not null && !admin.Can(permission))
        {
            return AdminResults.Error(StatusCodes.Status403Forbidden, "FORBIDDEN", "没有权限做这个操作");
        }

        return await next(context);
    }
}
