using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Persistence;

/// <summary>
/// 把具体数据库的上下文工厂（如 SQLite 的）当成通用上下文的工厂用：仓储和服务只依赖 <typeparamref name="TBase"/>，不关心是哪种数据库。
/// </summary>
/// <typeparam name="TBase">通用的上下文，如 <see cref="LabelDbContext"/>。</typeparam>
/// <typeparam name="TProvider">具体数据库的上下文，如 SqliteLabelDbContext。</typeparam>
/// <param name="inner">具体数据库的上下文工厂（EF Core 注册的）。</param>
internal sealed class DbContextFactoryAdapter<TBase, TProvider>(IDbContextFactory<TProvider> inner) : IDbContextFactory<TBase>
    where TBase : DbContext
    where TProvider : TBase
{
    /// <inheritdoc />
    public TBase CreateDbContext() => inner.CreateDbContext();

    /// <inheritdoc />
    public async Task<TBase> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        await inner.CreateDbContextAsync(cancellationToken);
}
