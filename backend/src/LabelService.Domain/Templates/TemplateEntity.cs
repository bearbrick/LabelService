namespace LabelService.Domain.Templates;

/// <summary>
/// 表 label_template：一种标签模板。内容在版本表里，这里只放列表要用的信息。
/// </summary>
public sealed class TemplateEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// 模板编码，全局唯一，开放接口用它找模板。
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// 模板名称，保存草稿时从模板内容同步。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 业务分组（business.id）。管理后台的模板库按它分组，用户和调用方按它授权。
    /// </summary>
    public int? BusinessId
    {
        get; set;
    }

    /// <summary>
    /// 状态，取值见 <see cref="TemplateStatuses"/>。
    /// </summary>
    public string Status { get; set; } = TemplateStatuses.Draft;

    /// <summary>
    /// 最新发布的版本号；从未发布时为 0。
    /// </summary>
    public int LatestVersion
    {
        get; set;
    }

    /// <summary>
    /// 创建人。
    /// </summary>
    public string CreatedBy { get; set; } = "";

    /// <summary>
    /// 最后修改人。
    /// </summary>
    public string UpdatedBy { get; set; } = "";

    /// <summary>
    /// 最后修改时间（UTC），保存草稿和发布时更新。
    /// </summary>
    public DateTime UpdatedAt
    {
        get; set;
    }

    /// <summary>
    /// 全部版本，包括草稿。
    /// </summary>
    public List<TemplateVersionEntity> Versions { get; set; } = [];
}
