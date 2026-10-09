using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Templates;
using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Templates;

/// <summary>
/// 模板的草稿、发布、回滚。给管理接口（T-014）和启动加载用，不在开放接口的请求路径上。
/// </summary>
/// <remarks>
/// 规则：同一模板同一时间最多一个草稿；发布生成新版本号并立即生效；发布后的版本内容不可修改。
/// 并发：两个人同时发布同一个草稿、或一人保存草稿时另一人刚好发布，后到的操作失败并提示重新打开。
/// </remarks>
internal sealed class TemplateRepository(
    IDbContextFactory<LabelDbContext> contexts,
    DbTemplateStore store,
    TemplatePublishValidator validator,
    TimeProvider clock,
    ILogger<TemplateRepository> logger)
{
    /// <summary>
    /// 模板列表，按编码排序。
    /// </summary>
    public async Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.Templates.AsNoTracking()
            .OrderBy(template => template.Code)
            .Select(template => new TemplateSummary(
                template.Code,
                template.Name,
                template.Category,
                template.Status,
                template.LatestVersion,
                template.Versions.Any(version => version.Status == TemplateStatuses.Draft),
                template.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取草稿内容；没有草稿时返回 null。
    /// </summary>
    public Task<LabelTemplate?> GetDraftAsync(string code, CancellationToken cancellationToken = default) =>
        GetContentAsync(code, version => version.Status == TemplateStatuses.Draft, cancellationToken);

    /// <summary>
    /// 取某个已发布版本的内容；不存在时返回 null。
    /// </summary>
    public Task<LabelTemplate?> GetVersionAsync(string code, int versionNo, CancellationToken cancellationToken = default) =>
        GetContentAsync(code, version => version.VersionNo == versionNo && version.Status == TemplateStatuses.Published, cancellationToken);

    /// <summary>
    /// 保存草稿：模板不存在时新建模板；已有草稿时覆盖，没有时新建（版本号 = 最新发布版本 + 1）。
    /// </summary>
    /// <param name="content">模板内容，编码取 <see cref="LabelTemplate.Code"/>。</param>
    /// <param name="user">操作人。</param>
    /// <param name="category">业务分组；null 表示不改。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>草稿的版本号。</returns>
    public async Task<int> SaveDraftAsync(LabelTemplate content, string user, string? category = null, CancellationToken cancellationToken = default)
    {
        if (!TemplatePublishValidator.IsValidCode(content.Code))
        {
            throw new TemplateStoreException($"模板编码“{content.Code}”不合法：要以字母开头，只含字母、数字、下划线、连字符，最长 64 个字符");
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var template = await db.Templates
            .Include(item => item.Versions.Where(version => version.Status == TemplateStatuses.Draft))
            .SingleOrDefaultAsync(item => item.Code == content.Code, cancellationToken);
        if (template is null)
        {
            template = new TemplateRecord { Code = content.Code, CreatedBy = user, Status = TemplateStatuses.Draft };
            db.Templates.Add(template);
        }

        template.Name = string.IsNullOrWhiteSpace(content.Name) ? content.Code : content.Name.Trim();
        template.Category = category ?? template.Category;
        template.UpdatedAt = now;
        var draft = template.Versions.SingleOrDefault();
        if (draft is null)
        {
            draft = new TemplateVersionRecord { VersionNo = template.LatestVersion + 1, Status = TemplateStatuses.Draft };
            template.Versions.Add(draft);
        }

        draft.ContentJson = JsonSerializer.Serialize(content, LabelJson.Options);
        await SaveAsync(db, "草稿刚被别人发布或新建了，请重新打开模板后再保存", cancellationToken);
        return draft.VersionNo;
    }

    /// <summary>
    /// 放弃草稿。没有草稿时返回 false。
    /// </summary>
    public async Task<bool> DiscardDraftAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var draft = await db.TemplateVersions
            .SingleOrDefaultAsync(version => version.Template!.Code == code && version.Status == TemplateStatuses.Draft, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        db.TemplateVersions.Remove(draft);
        await SaveAsync(db, "草稿刚被别人发布了，不能放弃", cancellationToken);
        return true;
    }

    /// <summary>
    /// 回滚的第一步：把某个已发布版本的内容复制成草稿（覆盖现有草稿），确认后再发布。
    /// </summary>
    /// <returns>草稿的版本号。</returns>
    public async Task<int> RestoreToDraftAsync(string code, int versionNo, string user, CancellationToken cancellationToken = default)
    {
        var content = await GetVersionAsync(code, versionNo, cancellationToken)
            ?? throw new TemplateStoreException($"模板 {code} 没有已发布的版本 {versionNo}");
        return await SaveDraftAsync(content, user, null, cancellationToken);
    }

    /// <summary>
    /// 发布草稿：检查、试渲染，通过后生成新版本并立即对开放接口生效。
    /// </summary>
    /// <exception cref="TemplateStoreException">没有草稿、检查不通过或并发冲突。检查不通过时 <see cref="TemplateStoreException.Details"/> 列出全部问题。</exception>
    public async Task<PublishResult> PublishAsync(string code, string user, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var template = await db.Templates
            .Include(item => item.Versions.Where(version => version.Status == TemplateStatuses.Draft))
            .SingleOrDefaultAsync(item => item.Code == code, cancellationToken)
            ?? throw new TemplateStoreException($"模板 {code} 不存在");
        var draft = template.Versions.SingleOrDefault()
            ?? throw new TemplateStoreException($"模板 {code} 没有草稿，没有可发布的内容");

        var content = Deserialize(draft.ContentJson);
        if (content.Code != code)
        {
            throw new TemplateStoreException($"草稿里的模板编码“{content.Code}”和模板 {code} 不一致");
        }

        var check = validator.Check(content);
        if (!check.CanPublish)
        {
            throw new TemplateStoreException($"模板 {code} 有 {check.Errors.Count} 个问题，不能发布", check.Errors);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        draft.Status = TemplateStatuses.Published;
        draft.PublishedBy = user;
        draft.PublishedAt = now;
        template.Status = TemplateStatuses.Published;
        template.LatestVersion = draft.VersionNo;
        template.UpdatedAt = now;
        await SaveAsync(db, "草稿刚被别人发布或修改了，请重新打开模板后再发布", cancellationToken);

        store.Add(code, new PublishedTemplate(content, draft.VersionNo));
        logger.LogInformation("模板 {Code} 发布版本 {Version}，操作人 {User}，试渲染 {RenderMs:0.0} ms", code, draft.VersionNo, user, check.RenderMs);
        return new PublishResult(draft.VersionNo, check.Warnings, check.RenderMs ?? 0);
    }

    /// <summary>
    /// 把数据库里的全部已发布版本加载进 <see cref="DbTemplateStore"/>。启动时调用一次。
    /// </summary>
    /// <returns>加载的版本数。</returns>
    public async Task<int> LoadPublishedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.TemplateVersions.AsNoTracking()
            .Where(version => version.Status == TemplateStatuses.Published)
            .Select(version => new { version.Template!.Code, version.VersionNo, version.ContentJson })
            .ToListAsync(cancellationToken);
        var published = new List<(string, PublishedTemplate)>(rows.Count);
        foreach (var row in rows)
        {
            try
            {
                published.Add((row.Code, new PublishedTemplate(Deserialize(row.ContentJson), row.VersionNo)));
            }
            catch (JsonException ex)
            {
                // 发布时校验过，读不出来说明库被改坏了。跳过这一个版本，别让整个服务起不来。
                logger.LogError(ex, "模板 {Code} 版本 {Version} 的内容无法解析，已跳过", row.Code, row.VersionNo);
            }
        }

        store.Load(published);
        return published.Count;
    }

    private async Task<LabelTemplate?> GetContentAsync(
        string code,
        System.Linq.Expressions.Expression<Func<TemplateVersionRecord, bool>> filter,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var json = await db.TemplateVersions.AsNoTracking()
            .Where(version => version.Template!.Code == code)
            .Where(filter)
            .Select(version => version.ContentJson)
            .SingleOrDefaultAsync(cancellationToken);
        return json is null ? null : Deserialize(json);
    }

    private static LabelTemplate Deserialize(string json) =>
        JsonSerializer.Deserialize<LabelTemplate>(json, LabelJson.Options) ?? throw new JsonException("模板内容为空");

    private static async Task SaveAsync(LabelDbContext db, string conflictMessage, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // 状态是并发令牌；(template_id, version_no) 唯一。两种冲突都是别人同时操作了同一个模板。
            throw new TemplateStoreException(conflictMessage, innerException: ex);
        }
    }
}

/// <summary>
/// 模板列表的一项。
/// </summary>
/// <param name="Code">模板编码。</param>
/// <param name="Name">模板名称。</param>
/// <param name="Category">业务分组。</param>
/// <param name="Status">draft（从未发布）或 published。</param>
/// <param name="LatestVersion">最新发布的版本号，从未发布时为 0。</param>
/// <param name="HasDraft">是否有未发布的草稿。</param>
/// <param name="UpdatedAt">最后修改时间（UTC）。</param>
internal sealed record TemplateSummary(string Code, string Name, string? Category, string Status, int LatestVersion, bool HasDraft, DateTime UpdatedAt);

/// <summary>
/// 发布结果。
/// </summary>
/// <param name="Version">新版本号。</param>
/// <param name="Warnings">提示，不影响发布，如试渲染偏慢、字段没有样例值。</param>
/// <param name="RenderMs">试渲染耗时，毫秒。</param>
internal sealed record PublishResult(int Version, IReadOnlyList<string> Warnings, double RenderMs);

/// <summary>
/// 模板操作失败：调用方能修正的问题（编码不合法、没有草稿、检查不通过、并发冲突）。管理接口转成 400 / 409。
/// </summary>
internal sealed class TemplateStoreException(string message, IReadOnlyList<string>? details = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// 全部问题，检查不通过时逐条列出。
    /// </summary>
    public IReadOnlyList<string> Details { get; } = details ?? [];
}
