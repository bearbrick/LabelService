using System.Text.Json;
using LabelService.Application.Templates.Dtos;
using LabelService.Contracts.Templates;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using Microsoft.Extensions.Logging;

namespace LabelService.Application.Templates;

/// <summary>
/// 模板的核心流程：保存草稿、放弃草稿、恢复历史版本、发布、换业务分组、删除，以及启动时加载已发布版本。
/// 不做权限和业务范围检查（由 <see cref="TemplateAppService"/> 做），启动导入示例模板时也用它。
/// </summary>
/// <remarks>
/// <para>规则：同一模板同一时间最多一个草稿；发布生成新版本号并立即生效；发布后的版本内容不可修改；删除是软删除（版本留档，编码不能再用）。</para>
/// <para>并发：两个人同时发布同一个草稿、或一人保存草稿时另一人刚好发布，后到的操作失败并提示重新打开。</para>
/// </remarks>
/// <param name="templates">模板仓储。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="cache">已发布模板的内存缓存，发布、换分组、删除后立即更新。</param>
/// <param name="checker">发布前检查。</param>
/// <param name="clock">时间来源。</param>
/// <param name="logger">日志。</param>
public sealed class TemplateManager(
    ITemplateRepository templates,
    IUnitOfWork unitOfWork,
    PublishedTemplateCache cache,
    TemplateChecker checker,
    TimeProvider clock,
    ILogger<TemplateManager> logger)
{
    /// <summary>
    /// 取草稿内容。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>草稿；没有草稿时为 null。</returns>
    public async Task<LabelTemplate?> GetDraftAsync(string code, CancellationToken cancellationToken = default) =>
        await templates.GetDraftContentAsync(code, cancellationToken) is { } json ? TemplateContent.Deserialize(json) : null;

    /// <summary>
    /// 取某个已发布版本的内容。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="versionNo">版本号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>内容；版本不存在或还是草稿时为 null。</returns>
    public async Task<LabelTemplate?> GetVersionAsync(string code, int versionNo, CancellationToken cancellationToken = default) =>
        await templates.GetPublishedContentAsync(code, versionNo, cancellationToken) is { } json ? TemplateContent.Deserialize(json) : null;

    /// <summary>
    /// 保存草稿：模板不存在时新建模板；已有草稿时覆盖，没有时新建（版本号 = 最新发布版本 + 1）。
    /// </summary>
    /// <param name="content">模板内容，编码取 <see cref="LabelTemplate.Code"/>。</param>
    /// <param name="user">操作人，如“张三（zhangsan）”。</param>
    /// <param name="businessId">业务分组；null 表示不改。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>草稿的版本号。</returns>
    /// <exception cref="UserFriendlyException">编码不合法、编码属于已删除的模板、并发冲突。</exception>
    public async Task<int> SaveDraftAsync(LabelTemplate content, string user, int? businessId = null, CancellationToken cancellationToken = default)
    {
        if (!TemplateCodes.IsValidCode(content.Code))
        {
            throw new UserFriendlyException($"模板编码“{content.Code}”不合法：{TemplateCodes.CodeRule}");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var template = await templates.FindWithDraftForUpdateAsync(content.Code, cancellationToken);
        if (template is { Status: TemplateStatuses.Deleted })
        {
            throw new UserFriendlyException($"编码 {content.Code} 属于已删除的模板，请换一个编码");
        }

        if (template is null)
        {
            template = new TemplateEntity { Code = content.Code, CreatedBy = user, Status = TemplateStatuses.Draft };
            templates.Add(template);
        }

        template.Name = string.IsNullOrWhiteSpace(content.Name) ? content.Code : content.Name.Trim();
        template.BusinessId = businessId ?? template.BusinessId;
        template.UpdatedBy = user;
        template.UpdatedAt = now;

        // 同一模板最多一个草稿：有就覆盖内容，没有就新建，版本号预先定为下一个发布版本号。
        var draft = template.Versions.SingleOrDefault(version => version.Status == TemplateStatuses.Draft);
        if (draft is null)
        {
            draft = new TemplateVersionEntity { VersionNo = template.LatestVersion + 1, Status = TemplateStatuses.Draft };
            template.Versions.Add(draft);
        }

        draft.ContentJson = TemplateContent.Serialize(content);
        await unitOfWork.SaveChangesAsync("草稿刚被别人发布或新建了，请重新打开模板后再保存", cancellationToken);
        return draft.VersionNo;
    }

    /// <summary>
    /// 放弃草稿。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>有草稿并已删除时为 true，没有草稿时为 false。</returns>
    /// <exception cref="UserFriendlyException">草稿刚被别人发布了。</exception>
    public async Task<bool> DiscardDraftAsync(string code, CancellationToken cancellationToken = default)
    {
        var draft = await templates.FindDraftForUpdateAsync(code, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        templates.RemoveVersion(draft);
        await unitOfWork.SaveChangesAsync("草稿刚被别人发布了，不能放弃", cancellationToken);
        return true;
    }

    /// <summary>
    /// 回滚的第一步：把某个已发布版本的内容复制成草稿（覆盖现有草稿），确认后再发布。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="versionNo">要恢复的版本号。</param>
    /// <param name="user">操作人。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>草稿的版本号。</returns>
    /// <exception cref="UserFriendlyException">没有这个已发布版本、并发冲突。</exception>
    public async Task<int> RestoreToDraftAsync(string code, int versionNo, string user, CancellationToken cancellationToken = default)
    {
        var content = await GetVersionAsync(code, versionNo, cancellationToken)
            ?? throw new UserFriendlyException($"模板 {code} 没有已发布的版本 {versionNo}");
        return await SaveDraftAsync(content, user, null, cancellationToken);
    }

    /// <summary>
    /// 发布草稿：检查、试渲染，通过后生成新版本并立即对开放接口生效。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="user">操作人。</param>
    /// <param name="note">发布说明。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新版本号、提示和试渲染耗时。</returns>
    /// <exception cref="UserFriendlyException">模板不存在、没有草稿、检查不通过（<see cref="UserFriendlyException.Details"/> 列出全部问题）或并发冲突。</exception>
    public async Task<PublishResultDto> PublishAsync(string code, string user, string note = "", CancellationToken cancellationToken = default)
    {
        var template = await templates.FindWithDraftForUpdateAsync(code, cancellationToken);
        if (template is null or { Status: TemplateStatuses.Deleted })
        {
            throw new UserFriendlyException($"模板 {code} 不存在");
        }

        var draft = template.Versions.SingleOrDefault(version => version.Status == TemplateStatuses.Draft)
            ?? throw new UserFriendlyException($"模板 {code} 没有草稿，没有可发布的内容");
        var content = TemplateContent.Deserialize(draft.ContentJson);
        if (content.Code != code)
        {
            throw new UserFriendlyException($"草稿里的模板编码“{content.Code}”和模板 {code} 不一致");
        }

        // 静态规则 + 样例值试渲染，有错误就不能发布。
        var check = checker.Check(content);
        if (!check.CanPublish)
        {
            throw new UserFriendlyException($"模板 {code} 有 {check.Errors.Count} 个问题，不能发布", check.Errors);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        draft.Status = TemplateStatuses.Published;
        draft.PublishedBy = user;
        draft.PublishedAt = now;
        draft.Note = (note ?? "").Trim();
        template.Status = TemplateStatuses.Published;
        template.LatestVersion = draft.VersionNo;
        template.UpdatedBy = user;
        template.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync("草稿刚被别人发布或修改了，请重新打开模板后再发布", cancellationToken);

        // 写库成功后才放进缓存，开放接口从下一个请求起用新版本。
        cache.Add(code, new PublishedTemplate(content, draft.VersionNo, template.BusinessId));
        logger.LogInformation("模板 {Code} 发布版本 {Version}，操作人 {User}，试渲染 {RenderMs:0.0} ms", code, draft.VersionNo, user, check.RenderMs);
        return new PublishResultDto(draft.VersionNo, check.Warnings, check.RenderMs ?? 0);
    }

    /// <summary>
    /// 换业务分组，对开放接口的调用方权限立即生效。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="businessId">新的业务分组；null 表示不分组。</param>
    /// <param name="user">操作人。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="UserFriendlyException">模板不存在、并发冲突。</exception>
    public async Task MoveAsync(string code, int? businessId, string user, CancellationToken cancellationToken = default)
    {
        var template = await templates.FindForUpdateAsync(code, cancellationToken)
            ?? throw new UserFriendlyException($"模板 {code} 不存在");
        template.BusinessId = businessId;
        template.UpdatedBy = user;
        template.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync("模板刚被别人修改了，请刷新后再试", cancellationToken);
        cache.SetBusiness(code, businessId);
    }

    /// <summary>
    /// 删除模板（软删除）：从列表和开放接口中移除，版本记录保留备查。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="user">操作人。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="UserFriendlyException">模板不存在、并发冲突。</exception>
    public async Task DeleteAsync(string code, string user, CancellationToken cancellationToken = default)
    {
        var template = await templates.FindForUpdateAsync(code, cancellationToken)
            ?? throw new UserFriendlyException($"模板 {code} 不存在");
        template.Status = TemplateStatuses.Deleted;
        template.UpdatedBy = user;
        template.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync("模板刚被别人修改了，请刷新后再试", cancellationToken);
        cache.Remove(code);
    }

    /// <summary>
    /// 把数据库里的全部已发布版本（不含已删除的模板）加载进内存缓存。启动时调用一次。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载的版本数。</returns>
    public async Task<int> LoadPublishedAsync(CancellationToken cancellationToken = default)
    {
        var rows = await templates.ListPublishedAsync(cancellationToken);
        var published = new List<(string, PublishedTemplate)>(rows.Count);
        foreach (var row in rows)
        {
            try
            {
                published.Add((row.Code, new PublishedTemplate(TemplateContent.Deserialize(row.ContentJson), row.VersionNo, row.BusinessId)));
            }
            catch (JsonException ex)
            {
                // 发布时校验过，读不出来说明库被改坏了。跳过这一个版本，别让整个服务起不来。
                logger.LogError(ex, "模板 {Code} 版本 {Version} 的内容无法解析，已跳过", row.Code, row.VersionNo);
            }
        }

        cache.Load(published);
        return published.Count;
    }
}
