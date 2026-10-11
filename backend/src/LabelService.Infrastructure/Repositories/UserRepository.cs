using LabelService.Domain.Identity;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 后台用户仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class UserRepository(LabelDbContext db) : IUserRepository
{
    /// <inheritdoc />
    public async Task<UserEntity?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        await db.Users.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    public Task<UserEntity?> FindByAccountAsync(string account, CancellationToken cancellationToken = default) =>
        db.Users.SingleOrDefaultAsync(user => user.Account == account, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().OrderBy(user => user.Account).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserEntity>> ListWithBusinessScopeAsync(CancellationToken cancellationToken = default) =>
        await db.Users.Where(user => !user.AllBusinesses && user.BusinessIds != "").ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => db.Users.AnyAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(user => user.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AccountExistsAsync(string account, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(user => user.Account == account, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, int>> CountByRoleAsync(CancellationToken cancellationToken = default) =>
        await db.Users.GroupBy(user => user.RoleId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

    /// <inheritdoc />
    public void Add(UserEntity user) => db.Users.Add(user);
}
