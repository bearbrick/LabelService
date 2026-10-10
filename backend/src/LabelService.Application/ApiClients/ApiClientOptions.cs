namespace LabelService.Application.ApiClients;

/// <summary>
/// 配置节 LabelService:Auth：写在配置文件里的调用方。
/// </summary>
public sealed class ApiClientOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Auth";

    /// <summary>
    /// 配置文件里的调用方。正式环境建议只用数据库里的调用方（管理后台维护），这里留空。
    /// </summary>
    public List<ApiClientConfigEntry> Clients { get; set; } = [];
}

/// <summary>
/// 配置文件里的一个调用方。
/// </summary>
public sealed class ApiClientConfigEntry
{
    /// <summary>
    /// 调用方名称，如 MES。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Key 的 SHA-256（十六进制）。配置里不写 Key 原文。
    /// </summary>
    public string KeySha256 { get; set; } = "";

    /// <summary>
    /// 允许使用的模板编码，* 表示全部。
    /// </summary>
    public List<string> AllowedTemplates { get; set; } = [];

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;
}
