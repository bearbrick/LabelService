using LabelService.Application.Identity;
using LabelService.Application.Logs;
using LabelService.Application.Shared;
using LabelService.Application.Templates.Dtos;
using LabelService.Contracts.Templates;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;

namespace LabelService.Application.Templates;

/// <summary>
/// 模板管理的应用服务实现：业务范围检查、操作日志和输出模型，核心流程交给 <see cref="TemplateManager"/>。
/// </summary>
/// <param name="templates">模板仓储（只读查询）。</param>
/// <param name="manager">模板的核心流程。</param>
/// <param name="checker">发布前检查。</param>
/// <param name="previews">缩略图。</param>
/// <param name="draftLogThrottle">保存草稿的操作日志节流。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="currentAdmin">当前用户。</param>
/// <param name="clock">时间来源。</param>
public sealed class TemplateAppService(
    ITemplateRepository templates,
    TemplateManager manager,
    TemplateChecker checker,
    TemplatePreviewRenderer previews,
    DraftLogThrottle draftLogThrottle,
    IOperationLogger operationLog,
    ICurrentAdmin currentAdmin,
    TimeProvider clock) : ITemplateAppService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var admin = currentAdmin.Principal;
        var rows = await templates.ListWithContentAsync(cancellationToken);
        return rows.Where(row => admin.InScope(row.Summary.BusinessId)).Select(row =>
        {
            var summary = row.Summary;
            var outline = TemplateContent.Outline(row.ContentJson);
            return new TemplateListItemDto(
                summary.Id,
                summary.Code,
                summary.Name,
                summary.BusinessId,
                summary.Status,
                summary.LatestVersion,
                summary.HasDraft,
                summary.CreatedBy,
                summary.UpdatedBy,
                summary.UpdatedAt,
                outline.Width,
                outline.Height,
                outline.Dpi,
                outline.FieldCount,
                outline.ElementCount);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<TemplateDetailDto> GetAsync(string code)
    {
        var template = await FindInScopeAsync(code);
        var draft = await manager.GetDraftAsync(code);
        var content = draft ?? await GetLatestAsync(template);
        return new TemplateDetailDto(
            template.Code,
            template.Name,
            template.BusinessId,
            template.Status,
            template.LatestVersion,
            draft is not null,
            template.UpdatedBy,
            template.UpdatedAt,
            content);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetThumbnailAsync(string code)
    {
        var template = await FindInScopeAsync(code);
        var content = await manager.GetDraftAsync(code) ?? await GetLatestAsync(template);

        // 缩略图按编码和修改时间缓存，模板一改就重新渲染。
        return content is null ? null : previews.Thumbnail(code, template.UpdatedAt, content);
    }

    /// <inheritdoc />
    public async Task<TemplateCreatedDto> CreateAsync(CreateTemplateDto input)
    {
        var admin = currentAdmin.Principal;
        if (!admin.InScope(input.BusinessId))
        {
            throw AdminErrors.ForbiddenError("不能在这个业务分组里新建模板");
        }

        var code = (input.Code ?? "").Trim();
        if (await templates.FindAsync(code) is not null)
        {
            throw new UserFriendlyException($"模板编码 {code} 已存在");
        }

        // 没给内容就建一个空白模板，默认 100×60 mm、300 DPI。
        var content = input.Content ?? new LabelTemplate { Page = new LabelPage { Width = input.Width ?? 100, Height = input.Height ?? 60, Dpi = input.Dpi ?? 300 } };
        content.Code = code;
        content.Name = string.IsNullOrWhiteSpace(input.Name) ? content.Name : input.Name.Trim();
        await manager.SaveDraftAsync(content, admin.DisplayName, input.BusinessId);
        await operationLog.WriteAsync("新建模板", $"{content.Code} {content.Name}", "新建模板");
        return new TemplateCreatedDto(content.Code);
    }

    /// <inheritdoc />
    public async Task<DraftSavedDto> SaveDraftAsync(string code, LabelTemplate content)
    {
        await FindInScopeAsync(code);
        if (content.Code != code)
        {
            throw new UserFriendlyException("模板编码不能在设计器里修改");
        }

        var admin = currentAdmin.Principal;
        var version = await manager.SaveDraftAsync(content, admin.DisplayName);
        var now = clock.GetUtcNow().UtcDateTime;
        if (draftLogThrottle.ShouldLog(admin.User.Id, code, now))
        {
            await operationLog.WriteAsync("编辑模板", $"{code} {content.Name}", "保存草稿");
        }

        return new DraftSavedDto(version, now);
    }

    /// <inheritdoc />
    public async Task DiscardDraftAsync(string code)
    {
        var template = await FindInScopeAsync(code);
        if (!await manager.DiscardDraftAsync(code))
        {
            throw new UserFriendlyException("没有未发布的草稿");
        }

        await operationLog.WriteAsync("编辑模板", Describe(template), "放弃未发布的改动");
    }

    /// <inheritdoc />
    public async Task<PublishResultDto> PublishAsync(string code, PublishTemplateDto? input)
    {
        var template = await FindInScopeAsync(code);
        var result = await manager.PublishAsync(code, currentAdmin.Principal.DisplayName, input?.Note ?? "");
        var note = string.IsNullOrWhiteSpace(input?.Note) ? "" : "：" + input.Note.Trim();
        await operationLog.WriteAsync("发布模板", Describe(template), $"发布 v{result.Version}{note}");
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateVersionDto>> ListVersionsAsync(string code)
    {
        await FindInScopeAsync(code);
        var versions = await templates.ListVersionsAsync(code);
        return versions.Select(item => new TemplateVersionDto(item.VersionNo, item.Status, item.PublishedBy, item.PublishedAt, item.Note)).ToList();
    }

    /// <inheritdoc />
    public async Task<LabelTemplate> GetVersionAsync(string code, int versionNo)
    {
        await FindInScopeAsync(code);
        return await manager.GetVersionAsync(code, versionNo)
            ?? throw AdminErrors.NotFoundError($"模板 {code} 没有已发布的版本 {versionNo}");
    }

    /// <inheritdoc />
    public async Task<DraftRestoredDto> RestoreAsync(string code, int versionNo)
    {
        var template = await FindInScopeAsync(code);
        var draft = await manager.RestoreToDraftAsync(code, versionNo, currentAdmin.Principal.DisplayName);
        await operationLog.WriteAsync("回滚版本", Describe(template), $"把 v{versionNo} 的内容恢复成草稿（发布后成为 v{draft}）");
        return new DraftRestoredDto(draft);
    }

    /// <inheritdoc />
    public async Task MoveAsync(string code, MoveTemplateDto input)
    {
        var template = await FindInScopeAsync(code);
        if (!currentAdmin.Principal.InScope(input.BusinessId))
        {
            throw AdminErrors.ForbiddenError("不能把模板移到自己范围以外的业务分组");
        }

        await manager.MoveAsync(code, input.BusinessId, currentAdmin.Principal.DisplayName);
        await operationLog.WriteAsync("编辑模板", Describe(template), "换了业务分组");
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string code)
    {
        var template = await FindInScopeAsync(code);
        await manager.DeleteAsync(code, currentAdmin.Principal.DisplayName);
        await operationLog.WriteAsync("删除模板", Describe(template), "删除模板，开放接口不能再调用");
    }

    /// <inheritdoc />
    public TemplateCheckResultDto Check(LabelTemplate content) => checker.Check(content);

    /// <summary>
    /// 操作日志里写的操作对象，如“BOX_LABEL 成品箱标”。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <returns>编码和名称。</returns>
    private static string Describe(TemplateEntity template) => $"{template.Code} {template.Name}";

    /// <summary>
    /// 按编码取当前用户业务范围里的模板（不跟踪）。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>模板。</returns>
    /// <exception cref="UserFriendlyException">不存在或不在范围里（NOT_FOUND，不暴露是否存在）。</exception>
    private async Task<TemplateEntity> FindInScopeAsync(string code)
    {
        var template = await templates.FindAsync(code);
        return template is not null && currentAdmin.Principal.InScope(template.BusinessId)
            ? template
            : throw AdminErrors.NotFoundError($"模板 {code} 不存在");
    }

    /// <summary>
    /// 最新发布版本的内容。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <returns>内容；从未发布时为 null。</returns>
    private async Task<LabelTemplate?> GetLatestAsync(TemplateEntity template) =>
        template.LatestVersion > 0 ? await manager.GetVersionAsync(template.Code, template.LatestVersion) : null;
}
