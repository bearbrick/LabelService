namespace LabelService.Client;

/// <summary>
/// 客户端设置，构造 <see cref="LabelServiceClient"/> 时传入，之后不要再修改。
/// </summary>
public sealed class LabelServiceClientOptions
{
    /// <summary>
    /// 服务地址，如 https://label.example.com/。
    /// </summary>
    public Uri? BaseAddress
    {
        get; set;
    }

    /// <summary>
    /// 调用方的 API Key。放配置文件或环境变量，不要写进代码。
    /// </summary>
    public string? ApiKey
    {
        get; set;
    }

    /// <summary>
    /// 单次请求超时，默认 30 秒。批量较大时适当调大。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 网络错误、超时和 502、503、504 的自动重试次数，默认 2。
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// 第一次重试前的等待时间，默认 0.5 秒；第 n 次重试等待 n 倍。
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// 批量单次总张数（含份数）上限，默认 500，和服务端配置保持一致。
    /// </summary>
    public int MaxLabelsPerRequest { get; set; } = 500;

    /// <summary>
    /// User-Agent，默认 LabelService.Client/版本号。
    /// </summary>
    public string? UserAgent
    {
        get; set;
    }
}
