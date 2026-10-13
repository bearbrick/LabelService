namespace LabelService.Client;

/// <summary>
/// 模板最新发布版本的字段定义和调用样例，用来生成录入表格或核对对接字段。
/// </summary>
public sealed class TemplateSchema
{
    /// <summary>
    /// 模板编码。
    /// </summary>
    public string TemplateCode { get; set; } = "";

    /// <summary>
    /// 模板显示名。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 最新发布版本号。
    /// </summary>
    public int Version
    {
        get; set;
    }

    /// <summary>
    /// 标签尺寸和默认 DPI。
    /// </summary>
    public TemplatePage Page { get; set; } = new TemplatePage();

    /// <summary>
    /// 字段定义。
    /// </summary>
    public List<TemplateField> Fields { get; set; } = new List<TemplateField>();

    /// <summary>
    /// 样例数据，可以直接作为请求数据使用（`request.Set(key, value)`）。值是 string、decimal、bool，JSON 对象、数组读成 Dictionary、List。
    /// </summary>
    public Dictionary<string, object?> Sample { get; set; } = new Dictionary<string, object?>();
}

/// <summary>
/// 模板列表的一项（<see cref="ILabelServiceClient.ListTemplatesAsync"/>）：编码、名称、最新发布版本和尺寸。
/// </summary>
public sealed class TemplateSummary
{
    /// <summary>
    /// 模板编码。
    /// </summary>
    public string TemplateCode { get; set; } = "";

    /// <summary>
    /// 模板显示名。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 最新发布版本号。
    /// </summary>
    public int Version
    {
        get; set;
    }

    /// <summary>
    /// 标签尺寸和默认 DPI。
    /// </summary>
    public TemplatePage Page { get; set; } = new TemplatePage();
}

/// <summary>
/// 标签尺寸。
/// </summary>
public sealed class TemplatePage
{
    /// <summary>
    /// 宽度（mm）。
    /// </summary>
    public double Width
    {
        get; set;
    }

    /// <summary>
    /// 高度（mm）。
    /// </summary>
    public double Height
    {
        get; set;
    }

    /// <summary>
    /// 模板默认 DPI。
    /// </summary>
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
    /// <summary>
    /// 字段 key，区分大小写。
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 中文显示名。
    /// </summary>
    public string? Name
    {
        get; set;
    }

    /// <summary>
    /// 类型：string、number、date、image。
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// 是否必填。
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 数据来源：input 仅接口传入，template 仅模板提供；null 保留旧版的接口优先规则。
    /// </summary>
    public string? Source
    {
        get; set;
    }

    /// <summary>
    /// 是否要求调用方填写；模板提供及旧版自动值字段不要求接口填写。
    /// </summary>
    public bool RequiresInput => Source != "template" && Required && Auto == null;

    /// <summary>
    /// 模板提供的手写值；旧版省略来源时作为不传值的回退。值为 string、decimal、bool 等，没有时为 null。
    /// </summary>
    public object? Default
    {
        get; set;
    }

    /// <summary>
    /// 系统预设：服务端计算 today（当前日期）、today+30d（30 天后）、index（批量里第几张）、client（调用方名称）等，
    /// 写法见 docs/contracts.md；没有时为 null。模板提供时忽略同名接口值，旧版省略来源时允许接口覆盖。
    /// </summary>
    public string? Auto
    {
        get; set;
    }

    /// <summary>
    /// number、date 的显示格式。
    /// </summary>
    public string? Format
    {
        get; set;
    }

    /// <summary>
    /// string 的最大长度。
    /// </summary>
    public int? MaxLength
    {
        get; set;
    }
}
