using LabelService.Domain.Identity;

namespace LabelService.Application.Identity;

/// <summary>
/// 管理后台的登录令牌（ADR 0006）。基础设施层用 HS256 JWT 实现，令牌里只放用户 ID，权限每次请求从库里读。
/// </summary>
public interface IAdminTokenService
{
    /// <summary>
    /// 签发令牌。勾选“7 天内自动登录”时 7 天，否则 8 小时。
    /// </summary>
    /// <param name="user">登录成功的用户。</param>
    /// <param name="remember">是否“7 天内自动登录”。</param>
    /// <returns>令牌和过期时间（UTC）。</returns>
    (string Token, DateTime ExpiresAt) Issue(UserEntity user, bool remember);
}
