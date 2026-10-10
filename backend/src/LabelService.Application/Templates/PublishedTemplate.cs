using LabelService.Contracts.Templates;

namespace LabelService.Application.Templates;

/// <summary>
/// 一个已发布的模板版本。内容只读，多个请求共享同一个实例。
/// </summary>
/// <param name="Template">模板内容。</param>
/// <param name="Version">版本号，从 1 开始。</param>
/// <param name="BusinessId">模板所属的业务分组；调用方按业务分组授权（ADR 0006）。从文件加载的模板为 null。</param>
public sealed record PublishedTemplate(LabelTemplate Template, int Version, int? BusinessId = null);
