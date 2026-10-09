using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Auth;

/// <summary>
/// 按 X-Api-Key 识别调用方。只保存 Key 的 SHA-256，查表在内存里完成，不访问数据库。
/// </summary>
/// <remarks>
/// 目前从配置读取调用方；api_client 表和管理界面完成后（T-009）改为启动时从数据库加载、变更时刷新。
/// </remarks>
internal sealed class ApiKeyAuthenticator
{
    private readonly Dictionary<string, ApiClient> clientsByHash;

    /// <summary>
    /// 从配置加载调用方；禁用的调用方不加载。
    /// </summary>
    public ApiKeyAuthenticator(IOptions<ApiClientOptions> options)
    {
        clientsByHash = options.Value.Clients
            .Where(client => client.Enabled && !string.IsNullOrWhiteSpace(client.KeySha256))
            .ToDictionary(
                client => client.KeySha256.Trim().ToLowerInvariant(),
                client => new ApiClient(client.Name, client.AllowedTemplates.ToHashSet(StringComparer.Ordinal)),
                StringComparer.Ordinal);
    }

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
    /// 计算 Key 的 SHA-256（小写十六进制），配置和数据库里只存这个值。
    /// </summary>
    public static string Hash(string apiKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
}

/// <summary>
/// 已识别的调用方。
/// </summary>
/// <param name="Name">调用方名称，写进调用日志和指标。</param>
/// <param name="AllowedTemplates">允许使用的模板编码；包含 * 表示全部。</param>
internal sealed record ApiClient(string Name, IReadOnlySet<string> AllowedTemplates)
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
    /// 调用方列表。
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
