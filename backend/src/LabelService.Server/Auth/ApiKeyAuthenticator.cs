using System.Collections.Frozen;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using LabelService.Server.Templates;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Auth;

/// <summary>
/// 按 X-Api-Key 识别调用方。只保存 Key 的 SHA-256，查表在内存里完成，不访问数据库（R3）。
/// </summary>
/// <remarks>
/// 调用方来自两处：配置文件（开发环境的测试 Key 等，按模板编码授权）和数据库 api_client 表（按业务分组授权、可设 IP 白名单，ADR 0006）。
/// 启动时加载，调用方变更（新建、改权限、停用、重置 Key）后由 <see cref="ApiClientRepository"/> 调 <see cref="Load"/> 整体替换快照，
/// 旧 Key 立即失效。读不加锁。同一个 Key 在两处都有时以数据库为准。
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
    public void Load(IEnumerable<ApiClientEntity> records)
    {
        var next = new Dictionary<string, ApiClient>(StringComparer.Ordinal);
        foreach (var client in configured.Where(client => client.Enabled && !string.IsNullOrWhiteSpace(client.KeySha256)))
        {
            var templates = client.AllowedTemplates.ToHashSet(StringComparer.Ordinal);
            next[client.KeySha256.Trim().ToLowerInvariant()] = new ApiClient(null, client.Name, templates.Contains("*"), FrozenSet<int>.Empty, templates, []);
        }

        foreach (var record in records.Where(record => record.Enabled))
        {
            next[record.ApiKeyHash] = new ApiClient(
                record.Id,
                record.Name,
                record.AllBusinesses,
                ParseIds(record.BusinessIds),
                FrozenSet<string>.Empty,
                ParseNetworks(record.IpAllowlist));
        }

        clientsByHash = next.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// 计算 Key 的 SHA-256（小写十六进制），配置和数据库里只存这个值。
    /// </summary>
    public static string Hash(string apiKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    /// <summary>
    /// 解析逗号分隔的 ID 列表，忽略非法项。
    /// </summary>
    public static IReadOnlySet<int> ParseIds(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => int.TryParse(item, out var id) ? id : (int?)null)
            .OfType<int>()
            .ToFrozenSet();

    /// <summary>
    /// 解析 IP 白名单：CIDR（10.20.1.0/24）或单个地址，逗号分隔。
    /// </summary>
    /// <exception cref="FormatException">有一项不是合法的地址或网段。</exception>
    public static IReadOnlyList<IPNetwork> ParseNetworks(string value)
    {
        var networks = new List<IPNetwork>();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPNetwork.TryParse(item, out var network))
            {
                networks.Add(network);
            }
            else if (IPAddress.TryParse(item, out var address))
            {
                networks.Add(new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
            }
            else
            {
                throw new FormatException($"“{item}”不是合法的 IP 地址或网段");
            }
        }

        return networks;
    }
}

/// <summary>
/// 已识别的调用方。
/// </summary>
/// <param name="Id">数据库里的调用方 ID；配置文件里的调用方为 null。</param>
/// <param name="Name">调用方名称，写进调用日志和指标。</param>
/// <param name="AllBusinesses">能调用全部模板。</param>
/// <param name="Businesses">能调用的业务分组 ID。</param>
/// <param name="Templates">能调用的模板编码（配置文件里的调用方用）。</param>
/// <param name="Networks">IP 白名单；空表示不限。</param>
internal sealed record ApiClient(
    int? Id,
    string Name,
    bool AllBusinesses,
    IReadOnlySet<int> Businesses,
    IReadOnlySet<string> Templates,
    IReadOnlyList<IPNetwork> Networks)
{
    /// <summary>
    /// 是否允许使用某个已发布的模板：全部、模板编码在列表里，或者模板所属的业务分组在授权范围里。
    /// </summary>
    public bool CanUse(PublishedTemplate template) =>
        AllBusinesses
        || Templates.Contains(template.Template.Code)
        || (template.BusinessId is { } business && Businesses.Contains(business));

    /// <summary>
    /// 来源 IP 是否在白名单里；没设白名单时总是允许。
    /// </summary>
    public bool AllowsIp(IPAddress? address)
    {
        if (Networks.Count == 0)
        {
            return true;
        }

        if (address is null)
        {
            return false;
        }

        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return Networks.Any(network => network.Contains(normalized));
    }
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
