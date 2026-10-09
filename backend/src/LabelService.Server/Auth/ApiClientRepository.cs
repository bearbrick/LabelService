using System.Security.Cryptography;
using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Auth;

/// <summary>
/// 调用方管理：新建、修改（业务范围、IP 白名单等）、启停、重置 Key。给管理接口（T-014）和启动加载用。
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
    public async Task<(ApiClientRecord Client, string ApiKey)> CreateAsync(ApiClientInput input, CancellationToken cancellationToken = default)
    {
        var key = NewKey();
        var now = clock.GetUtcNow().UtcDateTime;
        var client = new ApiClientRecord { ApiKeyHash = ApiKeyAuthenticator.Hash(key), KeyPrefix = key[..13], Enabled = true, CreatedAt = now, UpdatedAt = now };
        await using (var db = await contexts.CreateDbContextAsync(cancellationToken))
        {
            await ApplyAsync(db, client, input, cancellationToken);
            db.ApiClients.Add(client);
            await db.SaveChangesAsync(cancellationToken);
        }

        await ReloadAsync(cancellationToken);
        return (client, key);
    }

    /// <summary>
    /// 修改调用方：名称、AppId、说明、业务范围、IP 白名单；<paramref name="enabled"/> 为 null 时不改启停。
    /// </summary>
    public async Task<ApiClientRecord> UpdateAsync(int id, ApiClientInput? input, bool? enabled, CancellationToken cancellationToken = default)
    {
        ApiClientRecord client;
        await using (var db = await contexts.CreateDbContextAsync(cancellationToken))
        {
            client = await db.ApiClients.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                ?? throw new ManagementException($"调用方 {id} 不存在");
            if (input is not null)
            {
                await ApplyAsync(db, client, input, cancellationToken);
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
    public async Task<(ApiClientRecord Client, string ApiKey)> ResetKeyAsync(int id, CancellationToken cancellationToken = default)
    {
        var key = NewKey();
        ApiClientRecord client;
        await using (var db = await contexts.CreateDbContextAsync(cancellationToken))
        {
            client = await db.ApiClients.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                ?? throw new ManagementException($"调用方 {id} 不存在");
            client.ApiKeyHash = ApiKeyAuthenticator.Hash(key);
            client.KeyPrefix = key[..13];
            client.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }

        await ReloadAsync(cancellationToken);
        return (client, key);
    }

    /// <summary>
    /// 从数据库重新加载调用方到 <see cref="ApiKeyAuthenticator"/>。启动时和每次变更后调用。
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        authenticator.Load(await db.ApiClients.AsNoTracking().ToListAsync(cancellationToken));
    }

    private static async Task ApplyAsync(LabelDbContext db, ApiClientRecord client, ApiClientInput input, CancellationToken cancellationToken)
    {
        var name = input.Name.Trim();
        if (name.Length is 0 or > 64)
        {
            throw new ManagementException("调用方名称不能为空，最长 64 个字符");
        }

        if (await db.ApiClients.AnyAsync(item => item.Name == name && item.Id != client.Id, cancellationToken))
        {
            throw new ManagementException($"调用方 {name} 已存在");
        }

        var appId = input.AppId.Trim();
        if (appId.Length > 0 && await db.ApiClients.AnyAsync(item => item.AppId == appId && item.Id != client.Id, cancellationToken))
        {
            throw new ManagementException($"AppId {appId} 已被别的调用方使用");
        }

        try
        {
            ApiKeyAuthenticator.ParseNetworks(input.IpAllowlist);
        }
        catch (FormatException ex)
        {
            throw new ManagementException("IP 白名单格式不对：" + ex.Message);
        }

        var businesses = input.BusinessIds.Distinct().ToList();
        if (!input.AllBusinesses && businesses.Count > 0
            && await db.Businesses.CountAsync(item => businesses.Contains(item.Id), cancellationToken) != businesses.Count)
        {
            throw new ManagementException("选择的业务分组有的不存在");
        }

        client.Name = name;
        client.AppId = appId;
        client.Description = input.Description.Trim();
        client.AllBusinesses = input.AllBusinesses;
        client.BusinessIds = input.AllBusinesses ? "" : string.Join(",", businesses);
        client.IpAllowlist = string.Join(",", input.IpAllowlist.Split([',', '，', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// 生成 Key：正式环境 <c>lbl_live_</c>，其他环境 <c>lbl_test_</c>，后面 32 位随机字母数字（约 190 位熵）。
    /// </summary>
    private string NewKey()
    {
        var prefix = environment.IsProduction() ? "lbl_live_" : "lbl_test_";
        return prefix + RandomNumberGenerator.GetString(Alphabet, 32);
    }
}

/// <summary>
/// 新建、修改调用方时填的内容。
/// </summary>
/// <param name="Name">名称，全局唯一。</param>
/// <param name="AppId">调用方系统的标识，可空，非空时唯一。</param>
/// <param name="Description">说明。</param>
/// <param name="AllBusinesses">能调用全部业务分组的模板。</param>
/// <param name="BusinessIds">能调用的业务分组。</param>
/// <param name="IpAllowlist">IP 白名单，逗号分隔；空表示不限。</param>
internal sealed record ApiClientInput(string Name, string AppId, string Description, bool AllBusinesses, IReadOnlyList<int> BusinessIds, string IpAllowlist);
