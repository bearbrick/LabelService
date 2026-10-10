namespace LabelService.Domain.Identity;

/// <summary>
/// 后台用户的状态取值。
/// </summary>
public static class UserStatuses
{
    /// <summary>
    /// 正常。
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// 已停用：不能登录，已签发的令牌立即失效。
    /// </summary>
    public const string Disabled = "disabled";
}
