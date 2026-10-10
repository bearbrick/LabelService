using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using LabelService.Application.Identity;
using LabelService.Domain.Identity;
using LabelService.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LabelService.Infrastructure.Security;

/// <summary>
/// 管理后台的登录令牌（ADR 0006）：HS256 JWT，令牌里只放用户 ID，权限每次请求从库里读。
/// </summary>
public sealed class JwtAdminTokenService : IAdminTokenService
{
    /// <summary>
    /// 签发者和受众。
    /// </summary>
    public const string Issuer = "LabelService";

    /// <summary>
    /// 数据目录里自动生成的签名密钥文件名。不要提交到仓库。
    /// </summary>
    public const string KeyFileName = "admin-signing.key";

    /// <summary>
    /// 签名密钥。
    /// </summary>
    private readonly SymmetricSecurityKey key;

    /// <summary>
    /// 时间来源（检查程序可以替换）。
    /// </summary>
    private readonly TimeProvider clock;

    /// <summary>
    /// 读取签名密钥：配置 LabelService:Admin:SigningKey；没有时用数据目录里的 admin-signing.key，没有就生成一个。
    /// </summary>
    /// <param name="options">配置节 LabelService:Admin。</param>
    /// <param name="data">配置节 LabelService:Data（数据目录）。</param>
    /// <param name="clock">时间来源。</param>
    public JwtAdminTokenService(IOptions<AdminOptions> options, IOptions<DataOptions> data, TimeProvider clock)
    {
        this.clock = clock;
        key = new SymmetricSecurityKey(ResolveKey(options.Value, data.Value));
    }

    /// <summary>
    /// 校验令牌用的参数，接口层的 JWT 认证用它。
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

    /// <inheritdoc />
    public (string Token, DateTime ExpiresAt) Issue(UserEntity user, bool remember)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.Add(remember ? TimeSpan.FromDays(7) : TimeSpan.FromHours(8));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Issuer,

            // sub 是用户 ID，jti 让每次签发的令牌都不一样。
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    /// <summary>
    /// 签名密钥：配置里有就用配置的（至少 32 字节），否则读数据目录里的密钥文件，没有就生成一个存进去。
    /// </summary>
    /// <param name="options">管理后台配置。</param>
    /// <param name="data">数据目录配置。</param>
    /// <returns>密钥字节。</returns>
    private static byte[] ResolveKey(AdminOptions options, DataOptions data)
    {
        if (!string.IsNullOrWhiteSpace(options.SigningKey))
        {
            var configured = Convert.FromBase64String(options.SigningKey);
            return configured.Length >= 32 ? configured : throw new InvalidOperationException("LabelService:Admin:SigningKey 至少 32 字节（Base64）");
        }

        var directory = data.ResolveDirectory();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, KeyFileName);
        if (File.Exists(path))
        {
            return Convert.FromBase64String(File.ReadAllText(path).Trim());
        }

        var generated = RandomNumberGenerator.GetBytes(64);
        File.WriteAllText(path, Convert.ToBase64String(generated));
        return generated;
    }
}
