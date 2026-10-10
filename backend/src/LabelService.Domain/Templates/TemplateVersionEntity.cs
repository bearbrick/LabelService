namespace LabelService.Domain.Templates;

/// <summary>
/// 表 label_template_version：模板的一个版本，content_json 存整份模板 JSON。
/// </summary>
/// <remarks>
/// 草稿的版本号是“最新发布版本 + 1”，也就是发布后它会得到的号。(template_id, version_no) 唯一，
/// 所以同一模板最多只能有一个草稿。发布后的版本不能修改、不能删除，数据库上下文保存时会拦。
/// </remarks>
public sealed class TemplateVersionEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// 所属模板的 ID。
    /// </summary>
    public int TemplateId
    {
        get; set;
    }

    /// <summary>
    /// 所属模板。
    /// </summary>
    public TemplateEntity? Template
    {
        get; set;
    }

    /// <summary>
    /// 版本号，从 1 开始。
    /// </summary>
    public int VersionNo
    {
        get; set;
    }

    /// <summary>
    /// 整份模板 JSON，格式见 docs/contracts.md。
    /// </summary>
    public string ContentJson { get; set; } = "";

    /// <summary>
    /// 状态，取值见 <see cref="TemplateStatuses"/>。也是并发令牌：两个人同时发布同一个草稿，后到的会失败。
    /// </summary>
    public string Status { get; set; } = TemplateStatuses.Draft;

    /// <summary>
    /// 发布人；草稿为 null。
    /// </summary>
    public string? PublishedBy
    {
        get; set;
    }

    /// <summary>
    /// 发布时间（UTC）；草稿为 null。
    /// </summary>
    public DateTime? PublishedAt
    {
        get; set;
    }

    /// <summary>
    /// 发布说明，如“箱号改为第几箱 / 共几箱”。
    /// </summary>
    public string Note { get; set; } = "";
}
