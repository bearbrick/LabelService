using System.Text.Json;

namespace LabelService.Contracts.Templates;

/// <summary>
/// 模板需要的一项数据。元素通过 <c>{{key}}</c> 或 <c>{{key:format}}</c> 引用。
/// </summary>
public sealed class FieldDefinition
{
    /// <summary>
    /// 字段键：字母开头，只含字母、数字、下划线；模板内唯一，区分大小写。
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 中文显示名，设计器字段列表和演示程序录入表格里显示。
    /// </summary>
    public string? Name
    {
        get; set;
    }

    /// <summary>
    /// 值类型，决定校验规则和默认格式。
    /// </summary>
    public FieldType Type { get; set; } = FieldType.String;

    /// <summary>
    /// 是否必填，默认 true。必填字段没传、没有自动值也没有默认值时返回 FIELD_REQUIRED。
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 数据来源；省略时保持旧版接口优先的回退规则，显式来源只从对应位置取值。
    /// </summary>
    public FieldSource? Source
    {
        get; set;
    }

    /// <summary>
    /// 调用时没传则用这个值。类型必须和 <see cref="Type"/> 匹配。
    /// </summary>
    public JsonElement? Default
    {
        get; set;
    }

    /// <summary>
    /// 自动值：调用方没传时由服务端算出来，优先于 <see cref="Default"/>。按类型只认这些写法（ADR 0012）：
    /// date 用 today、now、today+30d、today-1d、today+6m、today+1y；number 用 index（第几条，从 1 开始）、total（共几条）；
    /// string 用 client（调用方名称）、requestId（请求号）。
    /// </summary>
    public string? Auto
    {
        get; set;
    }

    /// <summary>
    /// number、date 的默认显示格式（.NET 格式字符串）。绑定里写的格式优先。
    /// </summary>
    public string? Format
    {
        get; set;
    }

    /// <summary>
    /// 样例值，设计器预览、JSON 样例和发布前试渲染用。
    /// </summary>
    public JsonElement? Sample
    {
        get; set;
    }

    /// <summary>
    /// string 类型的最大长度（字符数），不设则不限制。
    /// </summary>
    public int? MaxLength
    {
        get; set;
    }
}

/// <summary>
/// 字段的显式数据来源，未指定来源的旧字段保持兼容行为。
/// </summary>
public enum FieldSource
{
    /// <summary>
    /// 只使用调用方条目或公共数据，不使用模板的自动值、默认值。
    /// </summary>
    Input,

    /// <summary>
    /// 只使用模板自动值或手写值，忽略同名接口输入，必填不适用。
    /// </summary>
    Template,
}

/// <summary>
/// 字段值类型。
/// </summary>
public enum FieldType
{
    /// <summary>
    /// 字符串。
    /// </summary>
    String,

    /// <summary>
    /// 数字，或能解析成 decimal 的字符串。
    /// </summary>
    Number,

    /// <summary>
    /// ISO 8601 日期或日期时间字符串，如 2026-10-08、2026-10-08T14:30:00。
    /// </summary>
    Date,

    /// <summary>
    /// PNG 或 JPG 的 base64，可带 data:image/png;base64, 前缀，解码后不超过 1 MB。
    /// </summary>
    Image,
}
