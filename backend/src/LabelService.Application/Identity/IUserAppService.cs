using LabelService.Application.Identity.Dtos;

namespace LabelService.Application.Identity;

/// <summary>
/// 用户管理（权限点 sys.user）。
/// </summary>
/// <remarks>
/// 规则和原型一致：不能改自己的角色、不能停用自己；新建用户和重置密码返回一次临时密码，用户首次登录后必须改。
/// </remarks>
public interface IUserAppService
{
    /// <summary>
    /// 全部用户，按账号排序。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户列表。</returns>
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新建用户，生成临时密码。
    /// </summary>
    /// <param name="input">账号、姓名、角色和业务范围。</param>
    /// <returns>新用户和临时密码。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">账号格式不对、账号已存在、姓名为空、角色或业务分组不存在。</exception>
    Task<UserCreatedDto> CreateAsync(CreateUserDto input);

    /// <summary>
    /// 修改姓名、角色和业务范围。
    /// </summary>
    /// <param name="id">用户 ID。</param>
    /// <param name="input">新的姓名、角色和业务范围。</param>
    /// <returns>修改后的用户。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">用户不存在（NOT_FOUND）、改自己的角色、内容不合法。</exception>
    Task<UserDto> UpdateAsync(int id, UpdateUserDto input);

    /// <summary>
    /// 启用或停用用户。停用立即生效：被停用的用户下一次请求就会被要求重新登录。
    /// </summary>
    /// <param name="id">用户 ID。</param>
    /// <param name="input">启用还是停用。</param>
    /// <returns>修改后的用户。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">停用自己、用户不存在（NOT_FOUND）。</exception>
    Task<UserDto> SetStatusAsync(int id, SetStatusInput input);

    /// <summary>
    /// 重置密码：生成新的临时密码，解除锁定，用户下次登录后必须改密码。
    /// </summary>
    /// <param name="id">用户 ID。</param>
    /// <returns>新的临时密码。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">用户不存在（NOT_FOUND）。</exception>
    Task<PasswordResetDto> ResetPasswordAsync(int id);
}
