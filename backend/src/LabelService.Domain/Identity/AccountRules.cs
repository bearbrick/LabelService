using System.Text.RegularExpressions;

namespace LabelService.Domain.Identity;

/// <summary>
/// 后台账号和姓名、角色名的格式规则。
/// </summary>
public static partial class AccountRules
{
    /// <summary>
    /// 账号规则的说明，校验不通过时给人看。
    /// </summary>
    public const string AccountRule = "账号只能用小写字母、数字、点、下划线、连字符，3–32 位，字母开头";

    /// <summary>
    /// 姓名、角色名的最大长度。
    /// </summary>
    public const int NameMaxLength = 64;

    /// <summary>
    /// 账号是否合法：小写字母开头，只能用小写字母、数字、点、下划线、连字符，3–32 位。
    /// </summary>
    /// <param name="account">已转成小写、去掉首尾空白的账号。</param>
    /// <returns>合法时为 true。</returns>
    public static bool IsValidAccount(string account) => AccountPattern().IsMatch(account);

    /// <summary>
    /// 姓名或角色名是否合法：不能为空，最长 <see cref="NameMaxLength"/> 字。
    /// </summary>
    /// <param name="name">已去掉首尾空白的名称。</param>
    /// <returns>合法时为 true。</returns>
    public static bool IsValidName(string name) => name.Length is > 0 and <= NameMaxLength;

    /// <summary>
    /// 账号的正则。
    /// </summary>
    /// <returns>编译好的正则。</returns>
    [GeneratedRegex("^[a-z][a-z0-9._-]{2,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountPattern();
}
