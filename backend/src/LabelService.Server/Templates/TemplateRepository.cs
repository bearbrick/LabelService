using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Templates;
using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Templates;

/// <summary>
/// 模板的草稿、发布、版本、回滚、换业务分组、删除。给管理接口（T-014）和启动加载用，不在开放接口的请求路径上。
/// </summary>
/// <remarks>
/// 规则：同一模板同一时间最多一个草稿；发布生成新版本号并立即生效；发布后的版本内容不可修改；删除是软删除（版本留档，编码不能再用）。
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
    /// 模板列表（不含已删除的），按编码排序。
    /// </summary>
    public async Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.Templates.AsNoTracking()
            .Where(template => template.Status != TemplateStatuses.Deleted)
            .OrderBy(template => template.Code)
            .Select(template => new TemplateSummary(
                template.Id,
                template.Code,
                template.Name,
                template.BusinessId,
                template.Status,
                template.LatestVersion,
                template.Versions.Any(version => version.Status == TemplateStatuses.Draft),
                template.CreatedBy,
                template.UpdatedBy,
                template.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 模板库用的列表：在 <see cref="ListAsync"/> 的基础上加上当前内容（有草稿取草稿，否则取最新发布版本）的画布和字段、元素个数。
    /// </summary>
    public async Task<IReadOnlyList<TemplateListItem>> ListWithOutlineAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.Templates.AsNoTracking()
            .Where(template => template.Status != TemplateStatuses.Deleted)
            .OrderBy(template => template.Code)
            .Select(template => new
            {
                Summary = new TemplateSummary(
                    template.Id,
                    template.Code,
                    template.Name,
                    template.BusinessId,
                    template.Status,
                    template.LatestVersion,
                    template.Versions.Any(version => version.Status == TemplateStatuses.Draft),
                    template.CreatedBy,
                    template.UpdatedBy,
                    template.UpdatedAt),
                Content = template.Versions
                    .Where(version => version.Status == TemplateStatuses.Draft || version.VersionNo == template.LatestVersion)
                    .OrderByDescending(version => version.VersionNo)
                    .Select(version => version.ContentJson)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new TemplateListItem(row.Summary, Outline(row.Content))).ToList();
    }

    /// <summary>
    /// 只读模板 JSON 里的画布和字段、元素个数，不做完整的反序列化。
    /// </summary>
    private static TemplateOutline Outline(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new TemplateOutline(0, 0, 0, 0, 0);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        double Number(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;
        int Count(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
        var page = root.TryGetProperty("page", out var p) ? p : default;
        return page.ValueKind == JsonValueKind.Object
            ? new TemplateOutline(Number(page, "width"), Number(page, "height"), (int)Number(page, "dpi"), Count("fields"), Count("elements"))
            : new TemplateOutline(0, 0, 0, Count("fields"), Count("elements"));
    }

    /// <summary>
    /// 按编码找模板（不含已删除的），管理接口用来核对业务范围。
    /// </summary>
    public async Task<TemplateRecord?> FindAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.Templates.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code && item.Status != TemplateStatuses.Deleted, cancellationToken);
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
    /// 全部版本（含草稿），新的在前。
    /// </summary>
    public async Task<IReadOnlyList<VersionSummary>> ListVersionsAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.TemplateVersions.AsNoTracking()
            .Where(version => version.Template!.Code == code)
            .OrderByDescending(version => version.VersionNo)
            .Select(version => new VersionSummary(version.VersionNo, version.Status, version.PublishedBy, version.PublishedAt, version.Note))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 保存草稿：模板不存在时新建模板；已有草稿时覆盖，没有时新建（版本号 = 最新发布版本 + 1）。
    /// </summary>
    /// <param name="content">模板内容，编码取 <see cref="LabelTemplate.Code"/>。</param>
    /// <param name="user">操作人。</param>
    /// <param name="businessId">业务分组；null 表示不改。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>草稿的版本号。</returns>
    public async Task<int> SaveDraftAsync(LabelTemplate content, string user, int? businessId = null, CancellationToken cancellationToken = default)
    {
        if (!TemplatePublishValidator.IsValidCode(content.Code))
        {
            throw new ManagementException($"模板编码“{content.Code}”不合法：要以字母开头，只含字母、数字、下划线、连字符，最长 64 个字符");
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var template = await db.Templates
            .Include(item => item.Versions.Where(version => version.Status == TemplateStatuses.Draft))
            .SingleOrDefaultAsync(item => item.Code == content.Code, cancellationToken);
        if (template is { Status: TemplateStatuses.Deleted })
        {
            throw new ManagementException($"编码 {content.Code} 属于已删除的模板，请换一个编码");
        }

        if (template is null)
        {
            template = new TemplateRecord { Code = content.Code, CreatedBy = user, Status = TemplateStatuses.Draft };
            db.Templates.Add(template);
        }

        template.Name = string.IsNullOrWhiteSpace(content.Name) ? content.Code : content.Name.Trim();
        template.BusinessId = businessId ?? template.BusinessId;
        template.UpdatedBy = user;
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
            ?? throw new ManagementException($"模板 {code} 没有已发布的版本 {versionNo}");
        return await SaveDraftAsync(content, user, null, cancellationToken);
    }

    /// <summary>
    /// 发布草稿：检查、试渲染，通过后生成新版本并立即对开放接口生效。
    /// </summary>
    /// <exception cref="ManagementException">没有草稿、检查不通过或并发冲突。检查不通过时 <see cref="ManagementException.Details"/> 列出全部问题。</exception>
    public async Task<PublishResult> PublishAsync(string code, string user, string note = "", CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var template = await db.Templates
            .Include(item => item.Versions.Where(version => version.Status == TemplateStatuses.Draft))
            .SingleOrDefaultAsync(item => item.Code == code && item.Status != TemplateStatuses.Deleted, cancellationToken)
            ?? throw new ManagementException($"模板 {code} 不存在");
        var draft = template.Versions.SingleOrDefault()
            ?? throw new ManagementException($"模板 {code} 没有草稿，没有可发布的内容");

        var content = Deserialize(draft.ContentJson);
        if (content.Code != code)
        {
            throw new ManagementException($"草稿里的模板编码“{content.Code}”和模板 {code} 不一致");
        }

        var check = validator.Check(content);
        if (!check.CanPublish)
        {
            throw new ManagementException($"模板 {code} 有 {check.Errors.Count} 个问题，不能发布", check.Errors);
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
        await SaveAsync(db, "草稿刚被别人发布或修改了，请重新打开模板后再发布", cancellationToken);

        store.Add(code, new PublishedTemplate(content, draft.VersionNo, template.BusinessId));
        logger.LogInformation("模板 {Code} 发布版本 {Version}，操作人 {User}，试渲染 {RenderMs:0.0} ms", code, draft.VersionNo, user, check.RenderMs);
        return new PublishResult(draft.VersionNo, check.Warnings, check.RenderMs ?? 0);
    }

    /// <summary>
    /// 换业务分组，对开放接口的调用方权限立即生效。
    /// </summary>
    public async Task MoveAsync(string code, int? businessId, string user, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var template = await db.Templates.SingleOrDefaultAsync(item => item.Code == code && item.Status != TemplateStatuses.Deleted, cancellationToken)
            ?? throw new ManagementException($"模板 {code} 不存在");
        template.BusinessId = businessId;
        template.UpdatedBy = user;
        template.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await SaveAsync(db, "模板刚被别人修改了，请刷新后再试", cancellationToken);
        store.SetBusiness(code, businessId);
    }

    /// <summary>
    /// 删除模板（软删除）：从列表和开放接口中移除，版本记录保留备查。
    /// </summary>
    public async Task DeleteAsync(string code, string user, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var template = await db.Templates.SingleOrDefaultAsync(item => item.Code == code && item.Status != TemplateStatuses.Deleted, cancellationToken)
            ?? throw new ManagementException($"模板 {code} 不存在");
        template.Status = TemplateStatuses.Deleted;
        template.UpdatedBy = user;
        template.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await SaveAsync(db, "模板刚被别人修改了，请刷新后再试", cancellationToken);
        store.Remove(code);
    }

    /// <summary>
    /// 把数据库里的全部已发布版本（不含已删除的模板）加载进 <see cref="DbTemplateStore"/>。启动时调用一次。
    /// </summary>
    /// <returns>加载的版本数。</returns>
    public async Task<int> LoadPublishedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.TemplateVersions.AsNoTracking()
            .Where(version => version.Status == TemplateStatuses.Published && version.Template!.Status != TemplateStatuses.Deleted)
            .Select(version => new { version.Template!.Code, version.Template.BusinessId, version.VersionNo, version.ContentJson })
            .ToListAsync(cancellationToken);
        var published = new List<(string, PublishedTemplate)>(rows.Count);
        foreach (var row in rows)
        {
            try
            {
                published.Add((row.Code, new PublishedTemplate(Deserialize(row.ContentJson), row.VersionNo, row.BusinessId)));
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
            throw new ManagementException(conflictMessage, innerException: ex);
        }
    }
}

/// <summary>
/// 模板列表的一项。
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
internal sealed record TemplateSummary(
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
/// 模板的一个版本（列表用，不含内容）。
/// </summary>
/// <param name="VersionNo">版本号。</param>
/// <param name="Status">draft 或 published。</param>
/// <param name="PublishedBy">发布人。</param>
/// <param name="PublishedAt">发布时间（UTC）。</param>
/// <param name="Note">发布说明。</param>
internal sealed record VersionSummary(int VersionNo, string Status, string? PublishedBy, DateTime? PublishedAt, string Note);

/// <summary>
/// 发布结果。
/// </summary>
/// <param name="Version">新版本号。</param>
/// <param name="Warnings">提示，不影响发布，如试渲染偏慢、字段没有样例值。</param>
/// <param name="RenderMs">试渲染耗时，毫秒。</param>
internal sealed record PublishResult(int Version, IReadOnlyList<string> Warnings, double RenderMs);

/// <summary>
/// 模板库列表的一项：概要加上当前内容的轮廓。
/// </summary>
/// <param name="Summary">概要。</param>
/// <param name="Outline">画布和字段、元素个数。</param>
internal sealed record TemplateListItem(TemplateSummary Summary, TemplateOutline Outline);

/// <summary>
/// 模板当前内容（草稿或最新发布版本）的轮廓。
/// </summary>
/// <param name="Width">画布宽度，mm。</param>
/// <param name="Height">画布高度，mm。</param>
/// <param name="Dpi">分辨率。</param>
/// <param name="FieldCount">字段个数。</param>
/// <param name="ElementCount">元素个数。</param>
internal sealed record TemplateOutline(double Width, double Height, int Dpi, int FieldCount, int ElementCount);
