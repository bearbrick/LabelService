using System.Diagnostics.CodeAnalysis;
using LabelService.Contracts.Templates;

namespace LabelService.Server.Templates;

/// <summary>
/// 已发布模板的读取入口。开放接口只通过它取模板，实现必须是内存查找，不能在请求路径上访问数据库或磁盘。
/// </summary>
internal interface ITemplateStore
{
    /// <summary>
    /// 取已发布的模板版本。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="version">版本号；null 表示最新发布版本。</param>
    /// <param name="template">找到的模板。</param>
    /// <returns>模板不存在、版本不存在或从未发布时返回 false。</returns>
    bool TryGetPublished(string templateCode, int? version, [NotNullWhen(true)] out PublishedTemplate? template);

    /// <summary>
    /// 每个模板的最新发布版本（启动预热用）。
    /// </summary>
    IReadOnlyList<PublishedTemplate> LatestPublished();
}

/// <summary>
/// 一个已发布的模板版本。内容只读，多个请求共享同一个实例。
/// </summary>
/// <param name="Template">模板内容。</param>
/// <param name="Version">版本号，从 1 开始。</param>
/// <param name="BusinessId">模板所属的业务分组；调用方按业务分组授权（ADR 0006）。文件模板为 null。</param>
internal sealed record PublishedTemplate(LabelTemplate Template, int Version, int? BusinessId = null);
