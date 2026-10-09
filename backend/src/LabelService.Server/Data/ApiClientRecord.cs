namespace LabelService.Server.Data;

/// <summary>
/// 表 api_client：调用方（接入的业务系统），每个一把 API Key。Key 只存 SHA-256。
/// </summary>
internal sealed class ApiClientRecord
{
    /// <summary>主键，写进调用日志的 client_id。</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>调用方名称，如 MES、WMS，全局唯一。</summary>
    public string Name { get; set; } = "";

    /// <summary>API Key 的 SHA-256（小写十六进制）。重置 Key 时替换。</summary>
    public string ApiKeyHash { get; set; } = "";

    /// <summary>Key 的前几位（如 lbl_live_3Fk9），只用来在后台辨认是哪把 Key，不能用来鉴权。</summary>
    public string KeyPrefix { get; set; } = "";

    /// <summary>允许使用的模板编码，逗号分隔；<c>*</c> 表示全部。</summary>
    public string AllowedTemplates { get; set; } = "*";

    /// <summary>是否启用。停用后立即不能调用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt
    {
        get; set;
    }

    /// <summary>最后修改时间（UTC），改权限、启停、重置 Key 时更新。</summary>
    public DateTime UpdatedAt
    {
        get; set;
    }
}
