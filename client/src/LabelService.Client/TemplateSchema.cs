using System.Text.Json;

namespace LabelService.Client;

/// <summary>
/// 模板最新发布版本的字段定义和调用样例，用来生成录入表格或核对对接字段。
/// </summary>
public sealed class TemplateSchema
{
    /// <summary>模板编码。</summary>
    public string TemplateCode { get; set; } = "";

    /// <summary>模板显示名。</summary>
    public string Name { get; set; } = "";

    /// <summary>最新发布版本号。</summary>
    public int Version
    {
        get; set;
    }

    /// <summary>标签尺寸和默认 DPI。</summary>
    public TemplatePage Page { get; set; } = new TemplatePage();

    /// <summary>字段定义。</summary>
    public List<TemplateField> Fields { get; set; } = new List<TemplateField>();

    /// <summary>样例数据，可以直接作为请求数据使用。</summary>
    public Dictionary<string, JsonElement> Sample { get; set; } = new Dictionary<string, JsonElement>();
}

/// <summary>
/// 标签尺寸。
/// </summary>
public sealed class TemplatePage
{
    /// <summary>宽度（mm）。</summary>
    public double Width
    {
        get; set;
    }

    /// <summary>高度（mm）。</summary>
    public double Height
    {
        get; set;
    }

    /// <summary>模板默认 DPI。</summary>
    public int Dpi
    {
        get; set;
    }
}

/// <summary>
/// 一个字段的定义。
/// </summary>
public sealed class TemplateField
{
    /// <summary>字段 key，区分大小写。</summary>
    public string Key { get; set; } = "";

    /// <summary>中文显示名。</summary>
    public string? Name
    {
        get; set;
    }

    /// <summary>类型：string、number、date、image。</summary>
    public string Type { get; set; } = "string";

    /// <summary>是否必填。</summary>
    public bool Required { get; set; } = true;

    /// <summary>不传时使用的默认值。</summary>
    public JsonElement? Default
    {
        get; set;
    }

    /// <summary>number、date 的显示格式。</summary>
    public string? Format
    {
        get; set;
    }

    /// <summary>string 的最大长度。</summary>
    public int? MaxLength
    {
        get; set;
    }
}
