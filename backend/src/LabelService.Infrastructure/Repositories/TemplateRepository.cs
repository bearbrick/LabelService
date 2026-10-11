using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 模板仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class TemplateRepository(LabelDbContext db) : ITemplateRepository
{
    /// <inheritdoc />
    public Task<TemplateEntity?> FindAsync(string code, CancellationToken cancellationToken = default) =>
        db.Templates.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code && item.Status != TemplateStatuses.Deleted, cancellationToken);

    /// <inheritdoc />
    public Task<TemplateEntity?> FindWithDraftForUpdateAsync(string code, CancellationToken cancellationToken = default) =>
        db.Templates
            .Include(item => item.Versions.Where(version => version.Status == TemplateStatuses.Draft))
            .SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

    /// <inheritdoc />
    public Task<TemplateEntity?> FindForUpdateAsync(string code, CancellationToken cancellationToken = default) =>
        db.Templates.SingleOrDefaultAsync(item => item.Code == code && item.Status != TemplateStatuses.Deleted, cancellationToken);

    /// <inheritdoc />
    public Task<TemplateVersionEntity?> FindDraftForUpdateAsync(string code, CancellationToken cancellationToken = default) =>
        db.TemplateVersions.SingleOrDefaultAsync(version => version.Template!.Code == code && version.Status == TemplateStatuses.Draft, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Templates.AsNoTracking()
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<(TemplateSummary Summary, string? ContentJson)>> ListWithContentAsync(CancellationToken cancellationToken = default)
    {
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

                // 当前内容：有草稿取草稿（版本号最大），否则取最新发布版本。
                Content = template.Versions
                    .Where(version => version.Status == TemplateStatuses.Draft || version.VersionNo == template.LatestVersion)
                    .OrderByDescending(version => version.VersionNo)
                    .Select(version => version.ContentJson)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => (row.Summary, row.Content)).ToList();
    }

    /// <inheritdoc />
    public Task<string?> GetDraftContentAsync(string code, CancellationToken cancellationToken = default) =>
        db.TemplateVersions.AsNoTracking()
            .Where(version => version.Template!.Code == code && version.Status == TemplateStatuses.Draft)
            .Select(version => version.ContentJson)
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<string?> GetPublishedContentAsync(string code, int versionNo, CancellationToken cancellationToken = default) =>
        db.TemplateVersions.AsNoTracking()
            .Where(version => version.Template!.Code == code && version.VersionNo == versionNo && version.Status == TemplateStatuses.Published)
            .Select(version => version.ContentJson)
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateVersionSummary>> ListVersionsAsync(string code, CancellationToken cancellationToken = default) =>
        await db.TemplateVersions.AsNoTracking()
            .Where(version => version.Template!.Code == code)
            .OrderByDescending(version => version.VersionNo)
            .Select(version => new TemplateVersionSummary(version.VersionNo, version.Status, version.PublishedBy, version.PublishedAt, version.Note))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublishedTemplateRow>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        await db.TemplateVersions.AsNoTracking()
            .Where(version => version.Status == TemplateStatuses.Published && version.Template!.Status != TemplateStatuses.Deleted)
            .Select(version => new PublishedTemplateRow(version.Template!.Code, version.Template.BusinessId, version.VersionNo, version.ContentJson))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => db.Templates.AnyAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, int>> CountByBusinessAsync(CancellationToken cancellationToken = default) =>
        await db.Templates.Where(template => template.Status != TemplateStatuses.Deleted && template.BusinessId != null)
            .GroupBy(template => template.BusinessId!.Value)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyInBusinessAsync(int businessId, CancellationToken cancellationToken = default) =>
        db.Templates.AnyAsync(item => item.BusinessId == businessId && item.Status != TemplateStatuses.Deleted, cancellationToken);

    /// <inheritdoc />
    public Task DetachBusinessAsync(int businessId, CancellationToken cancellationToken = default) =>
        db.Templates.Where(item => item.BusinessId == businessId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.BusinessId, (int?)null), cancellationToken);

    /// <inheritdoc />
    public void Add(TemplateEntity template) => db.Templates.Add(template);

    /// <inheritdoc />
    public void RemoveVersion(TemplateVersionEntity version) => db.TemplateVersions.Remove(version);
}
