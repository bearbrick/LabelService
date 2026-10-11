using LabelService.Domain.Businesses;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 业务分组仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class BusinessRepository(LabelDbContext db) : IBusinessRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<BusinessEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Businesses.AsNoTracking().OrderBy(item => item.Sort).ThenBy(item => item.Id).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<BusinessEntity?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        await db.Businesses.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    public Task<BusinessEntity?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.Businesses.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default) => db.Businesses.CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> CodeExistsAsync(string code, int exceptId, CancellationToken cancellationToken = default) =>
        db.Businesses.AnyAsync(item => item.Code == code && item.Id != exceptId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> AllExistAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0 || await db.Businesses.CountAsync(item => ids.Contains(item.Id), cancellationToken) == ids.Count;

    /// <inheritdoc />
    public void Add(BusinessEntity business) => db.Businesses.Add(business);

    /// <inheritdoc />
    public void Remove(BusinessEntity business) => db.Businesses.Remove(business);
}
