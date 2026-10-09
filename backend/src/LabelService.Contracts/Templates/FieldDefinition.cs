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
    /// 是否必填，默认 true。必填字段没传且没有默认值时返回 FIELD_REQUIRED。
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 调用时没传则用这个值。类型必须和 <see cref="Type"/> 匹配。
    /// </summary>
    public JsonElement? Default
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
/// 字段值类型。
/// </summary>
public enum FieldType
{
    /// <summary>字符串。</summary>
    String,

    /// <summary>数字，或能解析成 decimal 的字符串。</summary>
    Number,

    /// <summary>ISO 8601 日期或日期时间字符串，如 2026-10-08、2026-10-08T14:30:00。</summary>
    Date,

    /// <summary>PNG 或 JPG 的 base64，可带 data:image/png;base64, 前缀，解码后不超过 1 MB。</summary>
    Image,
}
