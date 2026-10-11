using LabelService.Domain.Identity;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 角色仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class RoleRepository(LabelDbContext db) : IRoleRepository
{
    /// <inheritdoc />
    public async Task<RoleEntity?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        await db.Roles.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Roles.AsNoTracking().OrderBy(role => role.Id).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => db.Roles.AnyAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        db.Roles.AnyAsync(role => role.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int exceptId, CancellationToken cancellationToken = default) =>
        db.Roles.AnyAsync(role => role.Name == name && role.Id != exceptId, cancellationToken);

    /// <inheritdoc />
    public Task<RoleEntity> GetAdministratorAsync(CancellationToken cancellationToken = default) =>
        db.Roles.Where(role => role.AllPermissions).OrderBy(role => role.Id).FirstAsync(cancellationToken);

    /// <inheritdoc />
    public void Add(RoleEntity role) => db.Roles.Add(role);

    /// <inheritdoc />
    public void Remove(RoleEntity role) => db.Roles.Remove(role);
}
