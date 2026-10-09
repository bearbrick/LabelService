namespace LabelService.Contracts.Protocol;

/// <summary>
/// 开放接口用到的 HTTP 头。客户端 SDK 里有同名副本，改动时两边一起改。
/// </summary>
public static class HeaderNames
{
    /// <summary>请求头：调用方的 API Key。</summary>
    public const string ApiKey = "X-Api-Key";

    /// <summary>请求头（可选）：调用方生成的请求号，服务端原样写进调用日志。</summary>
    public const string RequestId = "X-Request-Id";

    /// <summary>响应头：实际使用的模板版本号。</summary>
    public const string TemplateVersion = "X-Template-Version";

    /// <summary>响应头：文件里的标签张数（含份数）。</summary>
    public const string LabelCount = "X-Label-Count";

    /// <summary>响应头：实际使用的 DPI。</summary>
    public const string LabelDpi = "X-Label-Dpi";

    /// <summary>响应头：W3C traceparent 格式的链路 ID，排查问题时提供给服务方。</summary>
    public const string TraceId = "X-Trace-Id";

    /// <summary>响应头：服务端各阶段耗时，格式见 <see cref="TimingStages"/>。</summary>
    public const string ServerTiming = "Server-Timing";

    /// <summary>响应头：渲染警告，逗号分隔的 <c>CODE:detail</c>，detail 经过 URL 编码。</summary>
    public const string LabelWarnings = "X-Label-Warnings";
}
