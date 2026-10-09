namespace LabelService.Contracts.Templates;

/// <summary>
/// 一份完整的模板内容，对应一个模板版本的 content_json。设计器、数据库和渲染引擎之间只靠它传递模板。
/// </summary>
/// <remarks>
/// 长度单位一律是毫米，字号是磅（pt），颜色是 #RRGGBB。规范全文见 docs/contracts.md。
/// </remarks>
public sealed class LabelTemplate
{
    /// <summary>
    /// 当前规范版本。升级规范时新增版本号，渲染引擎按版本兼容旧模板。
    /// </summary>
    public const string CurrentSchemaVersion = "1.0";

    /// <summary>
    /// 模板 JSON 规范版本，缺省为 <see cref="CurrentSchemaVersion"/>。
    /// </summary>
    public string SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// 模板编码，全局唯一，就是开放接口里的 templateCode。只含大写字母、数字和下划线。
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// 模板显示名，如“成品箱标”。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 标签尺寸和默认分辨率。
    /// </summary>
    public LabelPage Page { get; set; } = new();

    /// <summary>
    /// 调用方需要传的字段。随模板版本一起发布，发布后不再变化。
    /// </summary>
    public List<FieldDefinition> Fields { get; set; } = [];

    /// <summary>
    /// 画布上的元素。数组顺序就是叠放顺序，后面的在上层。
    /// </summary>
    public List<LabelElement> Elements { get; set; } = [];
}
