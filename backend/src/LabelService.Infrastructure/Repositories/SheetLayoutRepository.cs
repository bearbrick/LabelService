using LabelService.Domain.Layouts;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 拼版版式仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class SheetLayoutRepository(LabelDbContext db) : ISheetLayoutRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SheetLayoutEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.SheetLayouts.AsNoTracking().OrderBy(item => item.Code).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SheetLayoutEntity>> ListEnabledAsync(CancellationToken cancellationToken = default) =>
        await db.SheetLayouts.AsNoTracking().Where(item => item.Enabled).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<SheetLayoutEntity?> FindAsync(string code, CancellationToken cancellationToken = default) =>
        db.SheetLayouts.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

    /// <inheritdoc />
    public Task<SheetLayoutEntity?> FindForUpdateAsync(string code, CancellationToken cancellationToken = default) =>
        db.SheetLayouts.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => db.SheetLayouts.AnyAsync(cancellationToken);

    /// <inheritdoc />
    public void Add(SheetLayoutEntity layout) => db.SheetLayouts.Add(layout);

    /// <inheritdoc />
    public void Remove(SheetLayoutEntity layout) => db.SheetLayouts.Remove(layout);
}
