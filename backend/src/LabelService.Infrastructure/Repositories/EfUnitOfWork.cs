using LabelService.Domain.Shared;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 工作单元的 EF Core 实现：保存这个请求里主库上下文的全部修改。
/// </summary>
/// <param name="db">这个请求的主库上下文（和仓储共用）。</param>
internal sealed class EfUnitOfWork(LabelDbContext db) : IUnitOfWork
{
    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task SaveChangesAsync(string conflictMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // 并发令牌不符（DbUpdateConcurrencyException）或违反唯一约束：都是别人同时改了同一条。
            throw new UserFriendlyException(conflictMessage, innerException: ex);
        }
    }
}
