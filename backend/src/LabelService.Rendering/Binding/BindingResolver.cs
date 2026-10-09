using System.Globalization;
using System.Text.RegularExpressions;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;

namespace LabelService.Rendering.Binding;

/// <summary>
/// 把文本里的 <c>{{key}}</c>、<c>{{key:format}}</c> 替换成实际值。
/// </summary>
/// <remarks>
/// 格式优先级：绑定里写的格式 → 字段定义的 format → 默认格式。一律用 InvariantCulture，结果不随服务器区域设置变化。
/// 值的运行时类型由 <see cref="Validation.FieldValidator"/> 保证：string、decimal、DateTime、byte[]（图片）或 null。
/// </remarks>
public static partial class BindingResolver
{
    /// <summary>
    /// 没有指定格式时日期的显示方式（时间为 0 点时）。
    /// </summary>
    public const string DefaultDateFormat = "yyyy-MM-dd";

    /// <summary>
    /// 没有指定格式时日期时间的显示方式。
    /// </summary>
    public const string DefaultDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 替换文本里的全部绑定。
    /// </summary>
    /// <param name="text">元素里的原文，如 <c>批次：{{batchNo}}</c>。</param>
    /// <param name="values">校验后的字段值。</param>
    /// <param name="fields">模板字段定义，按 key 索引。</param>
    /// <param name="warnings">引用了未定义字段时追加 BINDING_UNKNOWN 警告；传 null 则不记录。</param>
    /// <returns>替换后的文本。没有绑定时原样返回，不分配新字符串。</returns>
    public static string Resolve(
        string text,
        IReadOnlyDictionary<string, object?> values,
        IReadOnlyDictionary<string, FieldDefinition> fields,
        ICollection<string>? warnings = null)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{", StringComparison.Ordinal))
        {
            return text;
        }

        return BindingPattern().Replace(text, match =>
        {
            var key = match.Groups["key"].Value;
            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
            if (!fields.TryGetValue(key, out var field))
            {
                warnings?.Add(WarningCodes.UnknownBinding + ":" + Uri.EscapeDataString(key));
                return "";
            }

            values.TryGetValue(key, out var value);
            return FormatValue(value, format ?? field.Format);
        });
    }

    /// <summary>
    /// 列出文本里引用的全部字段 key，供保存模板时检查未定义字段。
    /// </summary>
    /// <param name="text">元素原文。</param>
    /// <returns>按出现顺序、去重后的 key。</returns>
    public static IReadOnlyList<string> FindKeys(string? text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{", StringComparison.Ordinal))
        {
            return [];
        }

        return BindingPattern().Matches(text)
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 按格式把一个字段值转成显示文本。
    /// </summary>
    /// <param name="value">字段值。</param>
    /// <param name="format">.NET 格式字符串，可空。格式非法时退回默认格式，不抛异常。</param>
    /// <returns>显示文本；null 和图片返回空字符串。</returns>
    public static string FormatValue(object? value, string? format)
    {
        try
        {
            return value switch
            {
                null => "",
                string text => text,
                decimal number => string.IsNullOrEmpty(format)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : number.ToString(format, CultureInfo.InvariantCulture),
                DateTime date => date.ToString(
                    string.IsNullOrEmpty(format) ? (date.TimeOfDay == TimeSpan.Zero ? DefaultDateFormat : DefaultDateTimeFormat) : format,
                    CultureInfo.InvariantCulture),
                byte[] => "",
                IFormattable formattable => formattable.ToString(format, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "",
            };
        }
        catch (FormatException)
        {
            return FormatValue(value, null);
        }
    }

    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*(?::(?<format>[^}]*))?\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex BindingPattern();
}
