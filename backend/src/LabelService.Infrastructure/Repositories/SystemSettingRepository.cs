using LabelService.Domain.SystemSettings;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Repositories;

/// <summary>
/// 系统设置仓储的 EF Core 实现。
/// </summary>
/// <param name="db">这个请求的主库上下文。</param>
internal sealed class SystemSettingRepository(LabelDbContext db) : ISystemSettingRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemSettingEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.SystemSettings.ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void Add(SystemSettingEntity setting) => db.SystemSettings.Add(setting);
}
