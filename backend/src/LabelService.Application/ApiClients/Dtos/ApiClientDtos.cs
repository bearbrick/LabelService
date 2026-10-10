namespace LabelService.Application.ApiClients.Dtos;

/// <summary>
/// 调用方（调用方与密钥页的一行）。不含 Key 哈希。
/// </summary>
/// <param name="Id">调用方 ID。</param>
/// <param name="Name">名称。</param>
/// <param name="AppId">调用方系统的标识。</param>
/// <param name="Description">说明。</param>
/// <param name="KeyPrefix">Key 前缀，用来辨认是哪把 Key。</param>
/// <param name="AllBusinesses">能调用全部业务分组的模板。</param>
/// <param name="BusinessIds">能调用的业务分组 ID，从小到大。</param>
/// <param name="IpAllowlist">IP 白名单，逗号分隔；空表示不限。</param>
/// <param name="Enabled">是否启用。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">最后修改时间（UTC）。</param>
public sealed record ApiClientDto(
    int Id,
    string Name,
    string AppId,
    string Description,
    string KeyPrefix,
    bool AllBusinesses,
    IReadOnlyList<int> BusinessIds,
    string IpAllowlist,
    bool Enabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// 新建调用方或重置 Key 的结果：调用方和 Key 原文（只返回这一次）。
/// </summary>
/// <param name="Client">调用方。</param>
/// <param name="ApiKey">Key 原文。</param>
public sealed record ApiClientKeyDto(ApiClientDto Client, string ApiKey);

/// <summary>
/// 新建或修改调用方。
/// </summary>
/// <param name="Name">名称，全局唯一，最长 64 个字符。</param>
/// <param name="AppId">调用方系统的标识，可空，非空时唯一。</param>
/// <param name="Description">说明。</param>
/// <param name="AllBusinesses">能调用全部业务分组的模板。</param>
/// <param name="BusinessIds">能调用的业务分组。</param>
/// <param name="IpAllowlist">IP 白名单：CIDR 网段或单个地址，逗号、换行、空格分隔；空表示不限。</param>
public sealed record SaveApiClientDto(string? Name, string? AppId, string? Description, bool AllBusinesses, int[]? BusinessIds, string? IpAllowlist);
