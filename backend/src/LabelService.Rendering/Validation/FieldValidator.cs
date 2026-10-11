using System.Globalization;
using System.Text.Json;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;

namespace LabelService.Rendering.Validation;

/// <summary>
/// 按模板字段定义校验调用方传来的数据，并转换成渲染用的值。
/// </summary>
/// <remarks>
/// 转换结果的类型：string → string，number → decimal，date → DateTime，image → byte[]，未传的可选字段 → null。
/// null、空字符串和全空白字符串都视为“没传”。多传的 key 直接忽略。
/// </remarks>
public static class FieldValidator
{
    /// <summary>
    /// 认的不带时区的日期写法（ISO 8601 及常见的空格分隔写法）。
    /// </summary>
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
    ];

    /// <summary>
    /// 认的带时区的日期写法（Z 或 +08:00）。
    /// </summary>
    private static readonly string[] DateOffsetFormats =
    [
        "yyyy-MM-ddTHH:mmK",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
    ];

    /// <summary>
    /// 校验一张标签的数据。
    /// </summary>
    /// <param name="fields">模板字段定义。</param>
    /// <param name="common">批量请求的公共字段；单张请求传 null。</param>
    /// <param name="data">这张标签的字段，同名时覆盖 <paramref name="common"/>。</param>
    /// <param name="index">批量条目序号（从 0 开始），写进错误明细；单张请求传 null。</param>
    /// <returns>转换后的值和全部错误，不会因为第一个错误就停止。</returns>
    public static FieldValidationResult Validate(
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyDictionary<string, JsonElement>? common,
        IReadOnlyDictionary<string, JsonElement>? data,
        int? index = null)
    {
        var values = new Dictionary<string, object?>(fields.Count, StringComparer.Ordinal);
        var errors = new List<ErrorDetail>();
        foreach (var field in fields)
        {
            try
            {
                ValidateField(field, common, data, index, values, errors);
            }
            catch (InvalidOperationException)
            {
                // JsonElement 读取字符串时才发现非法 UTF-8，按调用方数据错误处理，不能变成 500。
                errors.Add(Error(index, field.Key, ErrorCodes.FieldTypeInvalid, $"字段 {field.Key} 不是合法的 UTF-8 文本"));
            }
        }

        return new FieldValidationResult(values, errors);
    }

    /// <summary>
    /// 校验一个字段：取值（条目 → 公共字段 → 默认值），没值时检查必填，有值时转换类型。
    /// </summary>
    /// <param name="field">字段定义。</param>
    /// <param name="common">批量请求的公共字段。</param>
    /// <param name="data">这张标签的字段。</param>
    /// <param name="index">批量条目序号。</param>
    /// <param name="values">转换后的值，往里写。</param>
    /// <param name="errors">错误，往里追加。</param>
    private static void ValidateField(
        FieldDefinition field,
        IReadOnlyDictionary<string, JsonElement>? common,
        IReadOnlyDictionary<string, JsonElement>? data,
        int? index,
        Dictionary<string, object?> values,
        List<ErrorDetail> errors)
    {
        var raw = Pick(field.Key, data) ?? Pick(field.Key, common) ?? NotEmpty(field.Default);
        if (raw is null)
        {
            if (field.Required)
            {
                errors.Add(Error(index, field.Key, ErrorCodes.FieldRequired, $"缺少必填字段 {field.Key}"));
            }

            values[field.Key] = null;
            return;
        }

        var error = Convert(field, raw.Value, out var value);
        if (error is not null)
        {
            errors.Add(Error(index, field.Key, error.Value.Code, error.Value.Message));
            return;
        }

        values[field.Key] = value;
    }

    /// <summary>
    /// 从数据里取一个字段的值；空值当作没传。
    /// </summary>
    /// <param name="key">字段 key。</param>
    /// <param name="source">数据；可以为 null。</param>
    /// <returns>值；没传或是空值时为 null。</returns>
    private static JsonElement? Pick(string key, IReadOnlyDictionary<string, JsonElement>? source) =>
        source is not null && source.TryGetValue(key, out var value) ? NotEmpty(value) : null;

    /// <summary>
    /// null、未定义、空字符串和全空白字符串都当作没传。
    /// </summary>
    /// <param name="value">值。</param>
    /// <returns>有内容时原样返回，否则为 null。</returns>
    private static JsonElement? NotEmpty(JsonElement? value)
    {
        if (value is not { } element)
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String when string.IsNullOrWhiteSpace(element.GetString()) => null,
            _ => element,
        };
    }

    /// <summary>
    /// 按字段类型转换：字符串（数字、布尔也接受，转成文本）、数字（也接受数字字符串）、日期、图片。
    /// </summary>
    /// <param name="field">字段定义。</param>
    /// <param name="raw">调用方传的值。</param>
    /// <param name="value">转换后的值。</param>
    /// <returns>转换失败时的错误码和说明；成功时为 null。</returns>
    private static (string Code, string Message)? Convert(FieldDefinition field, JsonElement raw, out object? value)
    {
        value = null;
        switch (field.Type)
        {
            case FieldType.String:
                var text = raw.ValueKind switch
                {
                    JsonValueKind.String => raw.GetString(),
                    JsonValueKind.Number => raw.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null,
                };
                if (text is null)
                {
                    return (ErrorCodes.FieldTypeInvalid, $"字段 {field.Key} 应为字符串");
                }

                if (field.MaxLength is { } maxLength && text.Length > maxLength)
                {
                    return (ErrorCodes.FieldTypeInvalid, $"字段 {field.Key} 超过最大长度 {maxLength}");
                }

                value = text;
                return null;

            case FieldType.Number:
                if (raw.ValueKind == JsonValueKind.Number && raw.TryGetDecimal(out var number))
                {
                    value = number;
                    return null;
                }

                if (raw.ValueKind == JsonValueKind.String
                    && decimal.TryParse(raw.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    value = number;
                    return null;
                }

                return (ErrorCodes.FieldTypeInvalid, $"字段 {field.Key} 应为数字");

            case FieldType.Date:
                if (raw.ValueKind == JsonValueKind.String && TryParseDate(raw.GetString()!, out var date))
                {
                    value = date;
                    return null;
                }

                return (ErrorCodes.FieldTypeInvalid, $"字段 {field.Key} 应为 ISO 8601 日期，如 2026-10-08");

            case FieldType.Image:
                if (raw.ValueKind == JsonValueKind.String && TryDecodeImage(raw.GetString()!, out var bytes))
                {
                    value = bytes;
                    return null;
                }

                return (ErrorCodes.ImageInvalid, $"字段 {field.Key} 不是有效的 PNG 或 JPG base64，或超过 1 MB");

            default:
                return (ErrorCodes.FieldTypeInvalid, $"字段 {field.Key} 的类型 {field.Type} 不受支持");
        }
    }

    /// <summary>
    /// 解析日期：先按不带时区的写法，再按带时区的写法。
    /// </summary>
    /// <param name="text">日期文本。</param>
    /// <param name="date">解析出的日期（带时区的取调用方写的钟面时间）。</param>
    /// <returns>解析成功时为 true。</returns>
    private static bool TryParseDate(string text, out DateTime date)
    {
        text = text.Trim();
        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        // 带时区的值按调用方写的钟面时间显示，不换算到服务器时区。
        if (DateTimeOffset.TryParseExact(text, DateOffsetFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset))
        {
            date = offset.DateTime;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 解码图片字段：base64（可以带 data: 前缀），不超过 1 MB，文件头必须是 PNG 或 JPG。
    /// </summary>
    /// <param name="text">字段值。</param>
    /// <param name="bytes">解码出的图片字节。</param>
    /// <returns>是合法的 PNG 或 JPG 时为 true。</returns>
    private static bool TryDecodeImage(string text, out byte[] bytes)
    {
        bytes = [];
        var comma = text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? text.IndexOf(',') : -1;
        var base64 = comma >= 0 ? text.AsSpan(comma + 1).Trim() : text.AsSpan().Trim();

        // 先按长度拒绝，避免为超大字符串分配解码缓冲区。
        if (base64.Length > (RenderLimits.MaxImageBytes + 2) / 3 * 4)
        {
            return false;
        }

        var buffer = new byte[base64.Length * 3 / 4];
        if (!System.Convert.TryFromBase64Chars(base64, buffer, out var written) || written > RenderLimits.MaxImageBytes)
        {
            return false;
        }

        // 按文件头判断格式，不信调用方声明的类型。
        var span = buffer.AsSpan(0, written);
        var isPng = span.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]);
        var isJpeg = span.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]);
        if (!isPng && !isJpeg)
        {
            return false;
        }

        bytes = span.ToArray();
        return true;
    }

    /// <summary>
    /// 一条字段错误；批量时说明前面加“第 n 条”。
    /// </summary>
    /// <param name="index">批量条目序号；单张为 null。</param>
    /// <param name="field">字段 key。</param>
    /// <param name="code">错误码。</param>
    /// <param name="message">说明。</param>
    /// <returns>错误明细。</returns>
    private static ErrorDetail Error(int? index, string field, string code, string message) => new()
    {
        Index = index,
        Field = field,
        Code = code,
        Message = index is null ? message : $"第 {index + 1} 条{message}",
    };
}

/// <summary>
/// 一张标签的校验结果。
/// </summary>
/// <param name="Values">按字段 key 索引的转换后值，交给渲染引擎。</param>
/// <param name="Errors">全部错误；为空表示通过。</param>
public sealed record FieldValidationResult(IReadOnlyDictionary<string, object?> Values, IReadOnlyList<ErrorDetail> Errors)
{
    /// <summary>
    /// 是否通过校验。
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
