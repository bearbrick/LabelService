namespace LabelService.Server.Data;

/// <summary>
/// 表 canvas_preset：画布预设。选中预设后宽高写进模板，之后改预设不影响已有模板。
/// </summary>
internal sealed class CanvasPresetRecord
{
    /// <summary>主键。</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>显示名称，如“A4”“100×60”。</summary>
    public string Name { get; set; } = "";

    /// <summary>宽度，mm。</summary>
    public double WidthMm
    {
        get; set;
    }

    /// <summary>高度，mm。</summary>
    public double HeightMm
    {
        get; set;
    }

    /// <summary>排序，小的在前。</summary>
    public int Sort
    {
        get; set;
    }

    /// <summary>是否在设计器里显示。</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 表 label_template：一种标签。内容在版本表里，这里只放列表要用的信息。
/// </summary>
internal sealed class TemplateRecord
{
    /// <summary>主键。</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>模板编码，全局唯一，开放接口用它找模板。</summary>
    public string Code { get; set; } = "";

    /// <summary>模板名称，保存草稿时从模板内容同步。</summary>
    public string Name { get; set; } = "";

    /// <summary>业务分组，管理后台的模板库按它分组。</summary>
    public string? Category
    {
        get; set;
    }

    /// <summary>状态，取值见 <see cref="TemplateStatuses"/>。</summary>
    public string Status { get; set; } = TemplateStatuses.Draft;

    /// <summary>最新发布的版本号；从未发布时为 0。</summary>
    public int LatestVersion
    {
        get; set;
    }

    /// <summary>创建人。</summary>
    public string CreatedBy { get; set; } = "";

    /// <summary>最后修改时间（UTC），保存草稿和发布时更新。</summary>
    public DateTime UpdatedAt
    {
        get; set;
    }

    /// <summary>全部版本，包括草稿。</summary>
    public List<TemplateVersionRecord> Versions { get; set; } = [];
}

/// <summary>
/// 表 label_template_version：模板的一个版本，content_json 存整份模板 JSON。
/// </summary>
/// <remarks>
/// 草稿的版本号是“最新发布版本 + 1”，也就是发布后它会得到的号。(template_id, version_no) 唯一，
/// 所以同一模板最多只能有一个草稿。发布后的版本不能修改、不能删除，<see cref="LabelDbContext"/> 保存时会拦。
/// </remarks>
internal sealed class TemplateVersionRecord
{
    /// <summary>主键。</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>所属模板。</summary>
    public int TemplateId
    {
        get; set;
    }

    /// <summary>所属模板。</summary>
    public TemplateRecord? Template
    {
        get; set;
    }

    /// <summary>版本号，从 1 开始。</summary>
    public int VersionNo
    {
        get; set;
    }

    /// <summary>整份模板 JSON，格式见 docs/contracts.md。</summary>
    public string ContentJson { get; set; } = "";

    /// <summary>状态，取值见 <see cref="TemplateStatuses"/>。也是并发令牌：两个人同时发布同一个草稿，后到的会失败。</summary>
    public string Status { get; set; } = TemplateStatuses.Draft;

    /// <summary>发布人；草稿为 null。</summary>
    public string? PublishedBy
    {
        get; set;
    }

    /// <summary>发布时间（UTC）；草稿为 null。</summary>
    public DateTime? PublishedAt
    {
        get; set;
    }
}

/// <summary>
/// 表 asset：素材元数据。文件放在数据目录的 assets/ 下，表里只存元数据。
/// </summary>
internal sealed class AssetRecord
{
    /// <summary>主键。</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>种类：image、icon、font。</summary>
    public string Kind { get; set; } = "";

    /// <summary>显示名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>文件路径，相对于数据目录下的 assets/。</summary>
    public string FilePath { get; set; } = "";

    /// <summary>文件内容的 SHA-256（小写十六进制），用于去重和校验。</summary>
    public string Sha256 { get; set; } = "";

    /// <summary>文件大小，字节。</summary>
    public long Size
    {
        get; set;
    }

    /// <summary>上传时间（UTC）。</summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}

/// <summary>
/// 模板和版本的状态取值。
/// </summary>
internal static class TemplateStatuses
{
    /// <summary>草稿：模板从未发布过，或版本还没发布。</summary>
    public const string Draft = "draft";

    /// <summary>已发布：模板至少发布过一个版本，或版本已发布。</summary>
    public const string Published = "published";
}
