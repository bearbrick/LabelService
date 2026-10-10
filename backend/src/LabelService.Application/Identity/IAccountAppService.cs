using LabelService.Application.Identity.Dtos;

namespace LabelService.Application.Identity;

/// <summary>
/// 账号：登录、退出、当前用户、个人设置（改姓名手机号、改密码）、权限目录。
/// </summary>
public interface IAccountAppService
{
    /// <summary>
    /// 登录。连续输错 5 次锁 15 分钟；账号不存在时也算一次哈希，响应时间不暴露账号是否存在。
    /// </summary>
    /// <param name="input">账号、密码、是否 7 天内自动登录。</param>
    /// <returns>令牌、过期时间和当前用户。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">没填、密码不对（INVALID_LOGIN）、锁定（LOCKED）、停用（DISABLED）。</exception>
    Task<LoginResultDto> LoginAsync(LoginInput input);

    /// <summary>
    /// 退出登录（只记一条操作日志，令牌由前端丢弃）。
    /// </summary>
    /// <returns>完成时完成。</returns>
    Task LogoutAsync();

    /// <summary>
    /// 当前登录用户的信息。
    /// </summary>
    /// <returns>当前用户。</returns>
    CurrentUserDto GetCurrentUser();

    /// <summary>
    /// 修改自己的姓名和手机号。
    /// </summary>
    /// <param name="input">姓名和手机号。</param>
    /// <returns>完成时完成。</returns>
    Task UpdateProfileAsync(UpdateProfileInput input);

    /// <summary>
    /// 修改自己的密码；改完后不再要求先改密码。
    /// </summary>
    /// <param name="input">原密码和新密码。</param>
    /// <returns>完成时完成。</returns>
    Task ChangePasswordAsync(ChangePasswordInput input);

    /// <summary>
    /// 权限目录。
    /// </summary>
    /// <returns>全部权限点，按分组排列。</returns>
    IReadOnlyList<PermissionDto> ListPermissions();
}
