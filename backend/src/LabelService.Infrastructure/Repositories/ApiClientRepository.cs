using LabelService.Domain.ApiClients;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 调用方仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class ApiClientRepository(LabelDbContext db) : IApiClientRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiClientEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.ApiClients.AsNoTracking().OrderBy(client => client.Name).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ApiClientEntity?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        db.ApiClients.AsNoTracking().SingleOrDefaultAsync(client => client.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<ApiClientEntity?> FindForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        db.ApiClients.SingleOrDefaultAsync(client => client.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiClientEntity>> ListWithBusinessScopeForUpdateAsync(CancellationToken cancellationToken = default) =>
        await db.ApiClients.Where(client => !client.AllBusinesses && client.BusinessIds != "").ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int exceptId, CancellationToken cancellationToken = default) =>
        db.ApiClients.AnyAsync(client => client.Name == name && client.Id != exceptId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AppIdExistsAsync(string appId, int exceptId, CancellationToken cancellationToken = default) =>
        db.ApiClients.AnyAsync(client => client.AppId == appId && client.Id != exceptId, cancellationToken);

    /// <inheritdoc />
    public void Add(ApiClientEntity client) => db.ApiClients.Add(client);
}
