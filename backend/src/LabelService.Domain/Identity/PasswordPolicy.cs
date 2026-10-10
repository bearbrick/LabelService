namespace LabelService.Domain.Identity;

/// <summary>
/// 后台账号的密码规则和登录锁定规则（ADR 0006）。
/// </summary>
public static class PasswordPolicy
{
    /// <summary>
    /// 连续输错几次锁定。
    /// </summary>
    public const int MaxFailedLogins = 5;

    /// <summary>
    /// 锁定多久。
    /// </summary>
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// 检查新密码：至少 8 位，同时有字母和数字。
    /// </summary>
    /// <param name="password">新密码。</param>
    /// <returns>不符合时返回给人看的说明，符合时返回 null。</returns>
    public static string? Validate(string password) =>
        password.Length < 8 || !password.Any(char.IsAsciiLetter) || !password.Any(char.IsAsciiDigit)
            ? "密码至少 8 位，要同时有字母和数字"
            : null;
}
