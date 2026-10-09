using System.Security.Cryptography;
using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Auth;

/// <summary>
/// 调用方管理：新建、改权限、启停、重置 Key。给管理接口（T-014）和启动加载用。
/// </summary>
/// <remarks>
/// Key 只在新建和重置时返回一次原文，库里只存 SHA-256。每次变更后重新加载 <see cref="ApiKeyAuthenticator"/> 的快照，
/// 停用、重置立即生效（单实例，ADR 0003）。
/// </remarks>
internal sealed class ApiClientRepository(
    IDbContextFactory<LabelDbContext> contexts,
    ApiKeyAuthenticator authenticator,
    IHostEnvironment environment,
    TimeProvider clock)
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// 全部调用方，按名称排序。
    /// </summary>
    public async Task<IReadOnlyList<ApiClientRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.ApiClients.AsNoTracking().OrderBy(client => client.Name).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 新建调用方。返回记录和 Key 原文（只返回这一次）。
    /// </summary>
    /// <param name="name">名称，全局唯一。</param>
    /// <param name="allowedTemplates">允许的模板编码；<c>*</c> 表示全部。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<(ApiClientRecord Client, string ApiKey)> CreateAsync(string name, IEnumerable<string> allowedTemplates, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        if (name.Length is 0 or > 64)
        {
            throw new ManagementException("调用方名称不能为空，最长 64 个字符");
        }

        var key = NewKey();
        var now = clock.GetUtcNow().UtcDateTime;
        var client = new ApiClientRecord
        {
            Name = name,
            ApiKeyHash = ApiKeyAuthenticator.Hash(key),
            KeyPrefix = key[..Math.Min(13, key.Length)],
            AllowedTemplates = Join(allowedTemplates),
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await using (var db = await contexts.CreateDbContextAsync(cancellationToken))
        {
            if (await db.ApiClients.AnyAsync(item => item.Name == name, cancellationToken))
            {
                throw new ManagementException($"调用方 {name} 已存在");
            }

            db.ApiClients.Add(client);
            await db.SaveChangesAsync(cancellationToken);
        }

        await ReloadAsync(cancellationToken);
        return (client, key);
    }

    /// <summary>
    /// 修改调用方：允许的模板、启用状态。参数为 null 表示不改。
    /// </summary>
    public async Task<ApiClientRecord> UpdateAsync(int id, IEnumerable<string>? allowedTemplates, bool? enabled, CancellationToken cancellationToken = default)
    {
        ApiClientRecord client;
        await using (var db = await contexts.CreateDbContextAsync(cancellationToken))
        {
            client = await db.ApiClients.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                ?? throw new ManagementException($"调用方 {id} 不存在");
            if (allowedTemplates is not null)
            {
                client.AllowedTemplates = Join(allowedTemplates);
            }

            client.Enabled = enabled ?? client.Enabled;
            client.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }

        await ReloadAsync(cancellationToken);
        return client;
    }

    /// <summary>
    /// 重置 Key：生成新 Key，旧 Key 立即失效。返回新 Key 原文（只返回这一次）。
    /// </summary>
    public async Task<string> ResetKeyAsync(int id, CancellationToken cancellationToken = default)
    {
        var key = NewKey();
        await using (var db = await contexts.CreateDbContextAsync(cancellationToken))
        {
            var client = await db.ApiClients.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                ?? throw new ManagementException($"调用方 {id} 不存在");
            client.ApiKeyHash = ApiKeyAuthenticator.Hash(key);
            client.KeyPrefix = key[..Math.Min(13, key.Length)];
            client.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }

        await ReloadAsync(cancellationToken);
        return key;
    }

    /// <summary>
    /// 从数据库重新加载调用方到 <see cref="ApiKeyAuthenticator"/>。启动时和每次变更后调用。
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        authenticator.Load(await db.ApiClients.AsNoTracking().ToListAsync(cancellationToken));
    }

    /// <summary>
    /// 生成 Key：正式环境 <c>lbl_live_</c>，其他环境 <c>lbl_test_</c>，后面 32 位随机字母数字（约 190 位熵）。
    /// </summary>
    private string NewKey()
    {
        var prefix = environment.IsProduction() ? "lbl_live_" : "lbl_test_";
        return prefix + RandomNumberGenerator.GetString(Alphabet, 32);
    }

    private static string Join(IEnumerable<string> templates)
    {
        var list = templates.Select(code => code.Trim()).Where(code => code.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        return list.Count == 0 ? "" : string.Join(",", list);
    }
}
