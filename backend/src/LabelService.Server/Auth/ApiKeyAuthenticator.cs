using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using LabelService.Server.Data;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Auth;

/// <summary>
/// 按 X-Api-Key 识别调用方。只保存 Key 的 SHA-256，查表在内存里完成，不访问数据库（R3）。
/// </summary>
/// <remarks>
/// 调用方来自两处：配置文件（开发环境的测试 Key 等）和数据库 api_client 表。启动时加载，调用方变更（新建、改权限、
/// 停用、重置 Key）后由 <see cref="ApiClientRepository"/> 调 <see cref="Load"/> 整体替换快照，旧 Key 立即失效。
/// 读不加锁。同一个 Key 在两处都有时以数据库为准。
/// </remarks>
internal sealed class ApiKeyAuthenticator
{
    private readonly IReadOnlyList<ApiClientEntry> configured;
    private volatile FrozenDictionary<string, ApiClient> clientsByHash = FrozenDictionary<string, ApiClient>.Empty;

    /// <summary>
    /// 先加载配置里的调用方；数据库里的由启动初始化调用 <see cref="Load"/> 补上。
    /// </summary>
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
    public ApiClient? Authenticate(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return clientsByHash.GetValueOrDefault(Hash(apiKey.Trim()));
    }

    /// <summary>
    /// 用配置文件和数据库里的调用方重建快照。停用的不加载。
    /// </summary>
    public void Load(IEnumerable<ApiClientRecord> records)
    {
        var next = new Dictionary<string, ApiClient>(StringComparer.Ordinal);
        foreach (var client in configured.Where(client => client.Enabled && !string.IsNullOrWhiteSpace(client.KeySha256)))
        {
            next[client.KeySha256.Trim().ToLowerInvariant()] = new ApiClient(null, client.Name, client.AllowedTemplates.ToHashSet(StringComparer.Ordinal));
        }

        foreach (var record in records.Where(record => record.Enabled))
        {
            next[record.ApiKeyHash] = new ApiClient(record.Id, record.Name, ParseTemplates(record.AllowedTemplates));
        }

        clientsByHash = next.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// 计算 Key 的 SHA-256（小写十六进制），配置和数据库里只存这个值。
    /// </summary>
    public static string Hash(string apiKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    /// <summary>
    /// 解析逗号分隔的模板编码列表。
    /// </summary>
    public static IReadOnlySet<string> ParseTemplates(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
}

/// <summary>
/// 已识别的调用方。
/// </summary>
/// <param name="Id">数据库里的调用方 ID；配置文件里的调用方为 null。</param>
/// <param name="Name">调用方名称，写进调用日志和指标。</param>
/// <param name="AllowedTemplates">允许使用的模板编码；包含 * 表示全部。</param>
internal sealed record ApiClient(int? Id, string Name, IReadOnlySet<string> AllowedTemplates)
{
    /// <summary>
    /// 是否允许使用某个模板。
    /// </summary>
    public bool CanUse(string templateCode) =>
        AllowedTemplates.Contains("*") || AllowedTemplates.Contains(templateCode);
}

/// <summary>
/// 配置节 LabelService:Auth。
/// </summary>
internal sealed class ApiClientOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Auth";

    /// <summary>
    /// 配置文件里的调用方。正式环境建议只用数据库里的调用方（管理后台维护），这里留空。
    /// </summary>
    public List<ApiClientEntry> Clients { get; set; } = [];
}

/// <summary>
/// 配置里的一个调用方。
/// </summary>
internal sealed class ApiClientEntry
{
    /// <summary>调用方名称，如 MES。</summary>
    public string Name { get; set; } = "";

    /// <summary>Key 的 SHA-256（十六进制）。配置里不写 Key 原文。</summary>
    public string KeySha256 { get; set; } = "";

    /// <summary>允许使用的模板编码，* 表示全部。</summary>
    public List<string> AllowedTemplates { get; set; } = [];

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;
}
