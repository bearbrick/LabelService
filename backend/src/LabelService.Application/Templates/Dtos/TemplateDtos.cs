using LabelService.Contracts.Templates;

namespace LabelService.Application.Templates.Dtos;

/// <summary>
/// 模板库列表的一项：概要加上当前内容（有草稿取草稿，否则取最新发布版本）的画布、字段和元素个数。
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
/// <param name="Width">画布宽度，mm。</param>
/// <param name="Height">画布高度，mm。</param>
/// <param name="Dpi">分辨率。</param>
/// <param name="FieldCount">字段个数。</param>
/// <param name="ElementCount">元素个数。</param>
public sealed record TemplateListItemDto(
    int Id,
    string Code,
    string Name,
    int? BusinessId,
    string Status,
    int LatestVersion,
    bool HasDraft,
    string CreatedBy,
    string UpdatedBy,
    DateTime UpdatedAt,
    double Width,
    double Height,
    int Dpi,
    int FieldCount,
    int ElementCount);

/// <summary>
/// 打开模板的结果：模板信息和当前内容（有草稿时是草稿，没有时是最新发布版本）。
/// </summary>
/// <param name="Code">模板编码。</param>
/// <param name="Name">模板名称。</param>
/// <param name="BusinessId">业务分组。</param>
/// <param name="Status">draft 或 published。</param>
/// <param name="LatestVersion">最新发布的版本号。</param>
/// <param name="HasDraft">是否有未发布的草稿。</param>
/// <param name="UpdatedBy">最后修改人。</param>
/// <param name="UpdatedAt">最后修改时间（UTC）。</param>
/// <param name="Content">模板内容；从未保存过内容时为 null。</param>
public sealed record TemplateDetailDto(
    string Code,
    string Name,
    int? BusinessId,
    string Status,
    int LatestVersion,
    bool HasDraft,
    string UpdatedBy,
    DateTime UpdatedAt,
    LabelTemplate? Content);

/// <summary>
/// 新建模板：给完整内容，或者只给尺寸（空白模板）。
/// </summary>
/// <param name="Code">模板编码，全局唯一。</param>
/// <param name="Name">模板名称；空时用内容里的名称。</param>
/// <param name="BusinessId">业务分组，必须在自己的业务范围里。</param>
/// <param name="Width">空白模板的宽度，mm，默认 100。</param>
/// <param name="Height">空白模板的高度，mm，默认 60。</param>
/// <param name="Dpi">空白模板的分辨率，默认 300。</param>
/// <param name="Content">完整内容（如从文件导入、复制模板）；给了时忽略尺寸。</param>
public sealed record CreateTemplateDto(string? Code, string? Name, int? BusinessId, double? Width, double? Height, int? Dpi, LabelTemplate? Content);

/// <summary>
/// 新建模板的结果。
/// </summary>
/// <param name="Code">模板编码。</param>
public sealed record TemplateCreatedDto(string Code);

/// <summary>
/// 保存草稿的结果。
/// </summary>
/// <param name="DraftVersion">草稿的版本号（发布后就是这个版本号）。</param>
/// <param name="SavedAt">保存时间（UTC）。</param>
public sealed record DraftSavedDto(int DraftVersion, DateTime SavedAt);

/// <summary>
/// 把历史版本恢复成草稿的结果。
/// </summary>
/// <param name="DraftVersion">草稿的版本号。</param>
public sealed record DraftRestoredDto(int DraftVersion);

/// <summary>
/// 发布草稿。
/// </summary>
/// <param name="Note">发布说明，可空。</param>
public sealed record PublishTemplateDto(string? Note);

/// <summary>
/// 发布结果。
/// </summary>
/// <param name="Version">新版本号。</param>
/// <param name="Warnings">提示，不影响发布，如试渲染偏慢、字段没有样例值。</param>
/// <param name="RenderMs">试渲染耗时，毫秒。</param>
public sealed record PublishResultDto(int Version, IReadOnlyList<string> Warnings, double RenderMs);

/// <summary>
/// 换业务分组。
/// </summary>
/// <param name="BusinessId">新的业务分组；null 表示不分组（只有全部业务范围的用户能看）。</param>
public sealed record MoveTemplateDto(int? BusinessId);

/// <summary>
/// 模板的一个版本（版本记录列表的一行，不含内容）。
/// </summary>
/// <param name="VersionNo">版本号。</param>
/// <param name="Status">draft 或 published。</param>
/// <param name="PublishedBy">发布人；草稿为 null。</param>
/// <param name="PublishedAt">发布时间（UTC）；草稿为 null。</param>
/// <param name="Note">发布说明。</param>
public sealed record TemplateVersionDto(int VersionNo, string Status, string? PublishedBy, DateTime? PublishedAt, string Note);
