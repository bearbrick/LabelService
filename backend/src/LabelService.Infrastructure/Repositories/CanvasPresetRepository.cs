using LabelService.Domain.Resources;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 画布预设仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class CanvasPresetRepository(LabelDbContext db) : ICanvasPresetRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CanvasPresetEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.CanvasPresets.AsNoTracking().OrderBy(item => item.Sort).ThenBy(item => item.Id).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<CanvasPresetEntity?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        await db.CanvasPresets.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    public async Task<int> MaxSortAsync(CancellationToken cancellationToken = default) =>
        await db.CanvasPresets.MaxAsync(item => (int?)item.Sort, cancellationToken) ?? 0;

    /// <inheritdoc />
    public void Add(CanvasPresetEntity preset) => db.CanvasPresets.Add(preset);

    /// <inheritdoc />
    public void Remove(CanvasPresetEntity preset) => db.CanvasPresets.Remove(preset);
}
