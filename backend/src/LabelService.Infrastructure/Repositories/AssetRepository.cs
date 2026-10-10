using LabelService.Domain.Resources;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 素材元数据仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class AssetRepository(LabelDbContext db) : IAssetRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Assets.AsNoTracking().OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<AssetEntity?> FindByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        db.Assets.SingleOrDefaultAsync(item => item.Key == key, cancellationToken);

    /// <inheritdoc />
    public void Add(AssetEntity asset) => db.Assets.Add(asset);

    /// <inheritdoc />
    public void Remove(AssetEntity asset) => db.Assets.Remove(asset);
}
