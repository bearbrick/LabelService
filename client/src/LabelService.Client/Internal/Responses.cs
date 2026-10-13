using System.Globalization;

namespace LabelService.Client.Internal;

/// <summary>
/// 把 <see cref="JsonText.Parse"/> 读出的对象换成 SDK 的响应类型：字段定义、模板列表、错误响应体。
/// </summary>
/// <remarks>
/// 属性名先按原样找，找不到再忽略大小写找（和原来 System.Text.Json 的设置一样）；服务端多给的属性忽略。
/// 结构不对（比如该是对象的地方是数组）时抛 <see cref="FormatException"/>。
/// </remarks>
internal static class Responses
{
    /// <summary>
    /// 字段定义（GET /api/v1/templates/{code}/schema）。
    /// </summary>
    /// <param name="json">解析出的响应体。</param>
    /// <returns>字段定义。</returns>
    public static TemplateSchema ToSchema(object? json)
    {
        var body = Object(json, "字段定义");
        var schema = new TemplateSchema
        {
            TemplateCode = Text(body, "templateCode") ?? "",
            Name = Text(body, "name") ?? "",
            Version = Int(body, "version") ?? 0,
            Page = Page(Get(body, "page")),
        };
        foreach (var item in List(Get(body, "fields")))
        {
            var field = Object(item, "字段");
            schema.Fields.Add(new TemplateField
            {
                Key = Text(field, "key") ?? "",
                Name = Text(field, "name"),
                Type = Text(field, "type") ?? "string",
                Required = Bool(field, "required") ?? true,
                Source = Text(field, "source"),
                Default = Get(field, "default"),
                Format = Text(field, "format"),
                MaxLength = Int(field, "maxLength"),
                Auto = Text(field, "auto"),
            });
        }

        if (Get(body, "sample") is Dictionary<string, object?> sample)
        {
            foreach (var pair in sample)
            {
                schema.Sample[pair.Key] = pair.Value;
            }
        }

        return schema;
    }

    /// <summary>
    /// 模板列表（GET /api/v1/templates）；没有 templates 属性时是空列表。
    /// </summary>
    /// <param name="json">解析出的响应体。</param>
    /// <returns>模板列表。</returns>
    public static List<TemplateSummary> ToTemplateList(object? json)
    {
        var body = Object(json, "模板列表");
        var result = new List<TemplateSummary>();
        foreach (var item in List(Get(body, "templates")))
        {
            var template = Object(item, "模板");
            result.Add(new TemplateSummary
            {
                TemplateCode = Text(template, "templateCode") ?? "",
                Name = Text(template, "name") ?? "",
                Version = Int(template, "version") ?? 0,
                Page = Page(Get(template, "page")),
            });
        }

        return result;
    }

    /// <summary>
    /// 错误响应体：错误码、说明、逐条错误、跟踪 ID。
    /// </summary>
    /// <param name="json">解析出的响应体。</param>
    /// <returns>错误响应体；不是对象时为 null（按状态码处理）。</returns>
    public static ErrorBody? ToError(object? json)
    {
        if (!(json is Dictionary<string, object?> body))
        {
            return null;
        }

        var error = new ErrorBody
        {
            Code = Text(body, "code"),
            Message = Text(body, "message"),
            TraceId = Text(body, "traceId"),
        };
        if (Get(body, "errors") is List<object?> items)
        {
            error.Errors = new List<LabelFieldError>(items.Count);
            foreach (var item in items)
            {
                if (item is Dictionary<string, object?> detail)
                {
                    error.Errors.Add(new LabelFieldError
                    {
                        Index = Int(detail, "index"),
                        Field = Text(detail, "field"),
                        Code = Text(detail, "code") ?? "",
                        Message = Text(detail, "message"),
                    });
                }
            }
        }

        return error;
    }

    /// <summary>
    /// 标签尺寸；没有时是全 0。
    /// </summary>
    /// <param name="json">page 属性的值。</param>
    /// <returns>尺寸。</returns>
    private static TemplatePage Page(object? json)
    {
        if (!(json is Dictionary<string, object?> page))
        {
            return new TemplatePage();
        }

        return new TemplatePage
        {
            Width = Number(page, "width") ?? 0,
            Height = Number(page, "height") ?? 0,
            Dpi = Int(page, "dpi") ?? 0,
        };
    }

    /// <summary>
    /// 取属性：先按原样，再忽略大小写。
    /// </summary>
    /// <param name="body">对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>值；没有时为 null。</returns>
    private static object? Get(Dictionary<string, object?> body, string name)
    {
        if (body.TryGetValue(name, out var value))
        {
            return value;
        }

        foreach (var pair in body)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 要求是对象。
    /// </summary>
    /// <param name="json">值。</param>
    /// <param name="what">报错时说是什么。</param>
    /// <returns>对象。</returns>
    /// <exception cref="FormatException">不是对象。</exception>
    private static Dictionary<string, object?> Object(object? json, string what) =>
        json as Dictionary<string, object?> ?? throw new FormatException(what + "应该是 JSON 对象");

    /// <summary>
    /// 数组的元素；不是数组（包括没有这个属性）时为空。
    /// </summary>
    /// <param name="json">值。</param>
    /// <returns>元素。</returns>
    private static IEnumerable<object?> List(object? json) => json as List<object?> ?? new List<object?>();

    /// <summary>
    /// 字符串属性；数字等其他简单值按原文转成字符串。
    /// </summary>
    /// <param name="body">对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>字符串；没有或是 null 时为 null。</returns>
    private static string? Text(Dictionary<string, object?> body, string name)
    {
        var value = Get(body, name);
        return value == null || value is Dictionary<string, object?> || value is List<object?>
            ? null
            : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 数字属性。
    /// </summary>
    /// <param name="body">对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>数字；没有或不是数字时为 null。</returns>
    private static double? Number(Dictionary<string, object?> body, string name)
    {
        switch (Get(body, name))
        {
            case decimal exact:
                return (double)exact;
            case double approximate:
                return approximate;
            default:
                return null;
        }
    }

    /// <summary>
    /// 整数属性。
    /// </summary>
    /// <param name="body">对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>整数；没有、不是整数或超出 int 范围时为 null。</returns>
    private static int? Int(Dictionary<string, object?> body, string name) =>
        Get(body, name) is decimal value && value == decimal.Truncate(value) && value >= int.MinValue && value <= int.MaxValue ? (int)value : (int?)null;

    /// <summary>
    /// 布尔属性。
    /// </summary>
    /// <param name="body">对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>布尔值；没有或不是布尔值时为 null。</returns>
    private static bool? Bool(Dictionary<string, object?> body, string name) => Get(body, name) is bool value ? value : (bool?)null;
}
