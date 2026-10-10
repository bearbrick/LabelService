using LabelService.Domain.Shared;

namespace LabelService.Application.Shared;

/// <summary>
/// 管理接口的错误码，和创建对应异常的快捷方法。接口层按错误码决定 HTTP 状态码（见接口层的 AdminResults）。
/// </summary>
public static class AdminErrors
{
    /// <summary>
    /// 请求的内容不合法：400。
    /// </summary>
    public const string Invalid = UserFriendlyException.InvalidCode;

    /// <summary>
    /// 不存在，或不在自己的业务范围里（不暴露是否存在）：404。
    /// </summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>
    /// 没有权限做这个操作：403。
    /// </summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>
    /// 没登录、令牌过期或账号已停用：401。
    /// </summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>
    /// 必须先改密码（首次登录、管理员重置密码后）：403。
    /// </summary>
    public const string PasswordChangeRequired = "PASSWORD_CHANGE_REQUIRED";

    /// <summary>
    /// 登录时账号或密码不对：401。
    /// </summary>
    public const string InvalidLogin = "INVALID_LOGIN";

    /// <summary>
    /// 连续输错密码，账号暂时锁定：403。
    /// </summary>
    public const string Locked = "LOCKED";

    /// <summary>
    /// 账号已停用：403。
    /// </summary>
    public const string Disabled = "DISABLED";

    /// <summary>
    /// 创建“不存在”的异常。
    /// </summary>
    /// <param name="message">给人看的说明。</param>
    /// <returns>错误码为 <see cref="NotFound"/> 的异常。</returns>
    public static UserFriendlyException NotFoundError(string message) => new(message, code: NotFound);

    /// <summary>
    /// 创建“没有权限”的异常。
    /// </summary>
    /// <param name="message">给人看的说明。</param>
    /// <returns>错误码为 <see cref="Forbidden"/> 的异常。</returns>
    public static UserFriendlyException ForbiddenError(string message) => new(message, code: Forbidden);
}
