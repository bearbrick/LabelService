using System.Text.RegularExpressions;

namespace LabelService.Domain.Businesses;

/// <summary>
/// 业务分组的编码、名称、标识色规则。
/// </summary>
public static partial class BusinessRules
{
    /// <summary>
    /// 编码不合法时给人看的说明。
    /// </summary>
    public const string CodeRule = "编码要以大写字母开头，只含大写字母、数字、下划线，最长 32 位";

    /// <summary>
    /// 名称不合法时给人看的说明。
    /// </summary>
    public const string NameRule = "名称不能为空，最长 64 字";

    /// <summary>
    /// 把用户输入的编码规范化：去掉首尾空白、转大写。
    /// </summary>
    /// <param name="code">输入的编码。</param>
    /// <returns>规范化后的编码。</returns>
    public static string NormalizeCode(string? code) => (code ?? "").Trim().ToUpperInvariant();

    /// <summary>
    /// 编码是否合法（先 <see cref="NormalizeCode"/>）：大写字母开头，只含大写字母、数字、下划线，最长 32 位。
    /// </summary>
    /// <param name="code">规范化后的编码。</param>
    /// <returns>合法时为 true。</returns>
    public static bool IsValidCode(string code) => CodePattern().IsMatch(code);

    /// <summary>
    /// 名称是否合法：不为空，最长 64 字。
    /// </summary>
    /// <param name="name">去掉首尾空白后的名称。</param>
    /// <returns>合法时为 true。</returns>
    public static bool IsValidName(string name) => name.Length is > 0 and <= 64;

    /// <summary>
    /// 标识色是否合法：#RRGGBB。
    /// </summary>
    /// <param name="color">颜色。</param>
    /// <returns>合法时为 true。</returns>
    public static bool IsValidColor(string? color) => color is not null && ColorPattern().IsMatch(color);

    /// <summary>
    /// 编码的正则：大写字母开头，大写字母、数字、下划线，最长 32 位。
    /// </summary>
    /// <returns>编译好的正则。</returns>
    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    /// <summary>
    /// 标识色的正则：#RRGGBB。
    /// </summary>
    /// <returns>编译好的正则。</returns>
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
