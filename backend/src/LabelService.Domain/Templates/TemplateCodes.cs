using System.Text.RegularExpressions;

namespace LabelService.Domain.Templates;

/// <summary>
/// 模板编码和字段 key 的命名规则。编码会出现在接口路径和下载文件名里。
/// </summary>
public static partial class TemplateCodes
{
    /// <summary>
    /// 编码不合法时给人看的说明。
    /// </summary>
    public const string CodeRule = "要以字母开头，只含字母、数字、下划线、连字符，最长 64 个字符";

    /// <summary>
    /// 模板编码是否合法：字母开头，只含字母、数字、下划线、连字符，最长 64。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>合法时为 true。</returns>
    public static bool IsValidCode(string? code) => code is not null && CodePattern().IsMatch(code);

    /// <summary>
    /// 字段 key 是否合法：字母开头，只含字母、数字、下划线。
    /// </summary>
    /// <param name="key">字段 key。</param>
    /// <returns>合法时为 true。</returns>
    public static bool IsValidFieldKey(string? key) => key is not null && FieldKeyPattern().IsMatch(key);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldKeyPattern();
}
