using System.Collections.Frozen;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Shared;
using Microsoft.Extensions.Options;

namespace LabelService.Application.ApiClients;

/// <summary>
/// 按 X-Api-Key 识别调用方。只保存 Key 的 SHA-256，查表在内存里完成，请求路径上不访问数据库（AGENTS.md R3）。
/// </summary>
/// <remarks>
/// 调用方来自两处：配置文件（开发环境的测试 Key 等，按模板编码授权）和数据库 api_client 表（按业务分组授权、可设 IP 白名单，ADR 0006）。
/// 启动时加载，调用方变更（新建、改权限、停用、重置 Key）后整体替换快照，旧 Key 立即失效。读不加锁。同一个 Key 在两处都有时以数据库为准。
/// </remarks>
public sealed class ApiKeyAuthenticator
{
    /// <summary>
    /// 配置文件里的调用方。
    /// </summary>
    private readonly IReadOnlyList<ApiClientConfigEntry> configured;

    /// <summary>
    /// 当前快照：Key 的 SHA-256 → 调用方。
    /// </summary>
    private volatile FrozenDictionary<string, AuthenticatedClient> clientsByHash = FrozenDictionary<string, AuthenticatedClient>.Empty;

    /// <summary>
    /// 先加载配置里的调用方；数据库里的由启动初始化调用 <see cref="Load"/> 补上。
    /// </summary>
    /// <param name="options">配置节 LabelService:Auth。</param>
    public ApiKeyAuthenticator(IOptions<ApiClientOptions> options)
    {
        configured = options.Value.Clients;
        Load([]);
    }

    /// <summary>
    /// 当前可用的调用方数（含配置文件里的）。
    /// </summary>
    public int Count => clientsByHash.Count;

    /// <summary>
    /// 识别调用方。
    /// </summary>
    /// <param name="apiKey">请求头里的 Key 原文。</param>
    /// <returns>Key 无效、缺失或调用方已停用时返回 null。</returns>
    public AuthenticatedClient? Authenticate(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return clientsByHash.GetValueOrDefault(ApiKeys.Hash(apiKey.Trim()));
    }

    /// <summary>
    /// 用配置文件和数据库里的调用方重建快照。停用的不加载。
    /// </summary>
    /// <param name="entities">数据库里的全部调用方。</param>
    public void Load(IEnumerable<ApiClientEntity> entities)
    {
        var next = new Dictionary<string, AuthenticatedClient>(StringComparer.Ordinal);

        // 配置文件里的调用方：按模板编码授权，* 表示全部。
        foreach (var client in configured.Where(client => client.Enabled && !string.IsNullOrWhiteSpace(client.KeySha256)))
        {
            var templates = client.AllowedTemplates.ToHashSet(StringComparer.Ordinal);
            next[client.KeySha256.Trim().ToLowerInvariant()] = new AuthenticatedClient(null, client.Name, templates.Contains("*"), FrozenSet<int>.Empty, templates, []);
        }

        // 数据库里的调用方：按业务分组授权，后加载的覆盖同一个 Key。
        foreach (var entity in entities.Where(entity => entity.Enabled))
        {
            next[entity.ApiKeyHash] = new AuthenticatedClient(
                entity.Id,
                entity.Name,
                entity.AllBusinesses,
                IdList.Parse(entity.BusinessIds),
                FrozenSet<string>.Empty,
                IpAllowlist.Parse(entity.IpAllowlist));
        }

        clientsByHash = next.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
