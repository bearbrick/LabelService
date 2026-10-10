namespace LabelService.Domain.Templates;

/// <summary>
/// 模板列表的一项（只读查询的结果）。
/// </summary>
/// <param name="Id">模板 ID。</param>
/// <param name="Code">模板编码。</param>
/// <param name="Name">模板名称。</param>
/// <param name="BusinessId">业务分组。</param>
/// <param name="Status">draft（从未发布）或 published。</param>
/// <param name="LatestVersion">最新发布的版本号，从未发布时为 0。</param>
/// <param name="HasDraft">是否有未发布的草稿。</param>
/// <param name="CreatedBy">创建人。</param>
/// <param name="UpdatedBy">最后修改人。</param>
/// <param name="UpdatedAt">最后修改时间（UTC）。</param>
public sealed record TemplateSummary(
    int Id,
    string Code,
    string Name,
    int? BusinessId,
    string Status,
    int LatestVersion,
    bool HasDraft,
    string CreatedBy,
    string UpdatedBy,
    DateTime UpdatedAt);

/// <summary>
/// 模板的一个版本（列表用，不含内容）。接口 GET /api/admin/templates/{code}/versions 原样返回。
/// </summary>
/// <param name="VersionNo">版本号。</param>
/// <param name="Status">draft 或 published。</param>
/// <param name="PublishedBy">发布人；草稿为 null。</param>
/// <param name="PublishedAt">发布时间（UTC）；草稿为 null。</param>
/// <param name="Note">发布说明。</param>
public sealed record TemplateVersionSummary(int VersionNo, string Status, string? PublishedBy, DateTime? PublishedAt, string Note);

/// <summary>
/// 一个已发布版本的原始数据（启动时加载进内存缓存用）。
/// </summary>
/// <param name="Code">模板编码。</param>
/// <param name="BusinessId">业务分组。</param>
/// <param name="VersionNo">版本号。</param>
/// <param name="ContentJson">模板 JSON。</param>
public sealed record PublishedTemplateRow(string Code, int? BusinessId, int VersionNo, string ContentJson);
