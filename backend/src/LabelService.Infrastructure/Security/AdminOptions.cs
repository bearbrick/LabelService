namespace LabelService.Infrastructure.Security;

/// <summary>
/// 配置节 LabelService:Admin：管理后台的登录令牌和首次启动的管理员密码。
/// </summary>
public sealed class AdminOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Admin";

    /// <summary>
    /// JWT 签名密钥（Base64，至少 32 字节）。不配置时用数据目录里的 admin-signing.key。不要写进仓库。
    /// </summary>
    public string SigningKey { get; set; } = "";

    /// <summary>
    /// 首次启动时 admin 账号的密码。不配置时开发环境用 admin123，其他环境随机生成并写一条警告日志。
    /// </summary>
    public string InitialPassword { get; set; } = "";
}
