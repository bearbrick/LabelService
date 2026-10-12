using System.Globalization;

namespace LabelService.Client.Internal;

/// <summary>
/// HTTP 头名称。是 LabelService.Contracts.Protocol.HeaderNames 的副本，检查程序核对两边一致。
/// </summary>
internal static class HeaderNames
{
    /// <summary>
    /// 请求头：API Key。
    /// </summary>
    public const string ApiKey = "X-Api-Key";

    /// <summary>
    /// 请求头和响应头：调用方的链路 ID（SDK 每次调用新生成一个）。
    /// </summary>
    public const string RequestId = "X-Request-Id";

    /// <summary>
    /// 响应头：用的模板版本。
    /// </summary>
    public const string TemplateVersion = "X-Template-Version";

    /// <summary>
    /// 响应头：生成的标签张数。
    /// </summary>
    public const string LabelCount = "X-Label-Count";

    /// <summary>
    /// 响应头：输出的页数（拼版时一页放多张）。
    /// </summary>
    public const string PageCount = "X-Page-Count";

    /// <summary>
    /// 响应头：渲染用的分辨率。
    /// </summary>
    public const string LabelDpi = "X-Label-Dpi";

    /// <summary>
    /// 响应头：服务端的跟踪 ID，报问题时提供给运维。
    /// </summary>
    public const string TraceId = "X-Trace-Id";

    /// <summary>
    /// 响应头：各阶段耗时（W3C Server-Timing）。
    /// </summary>
    public const string ServerTiming = "Server-Timing";

    /// <summary>
    /// 响应头：渲染警告码，逗号分隔，如字体降级。
    /// </summary>
    public const string LabelWarnings = "X-Label-Warnings";
}

/// <summary>
/// 解析 Server-Timing 头，如 <c>auth;dur=0.3, draw;dur=18.5, total;dur=46.1</c>。
/// </summary>
internal static class ServerTimingParser
{
    /// <summary>
    /// 解析成“阶段 → 毫秒”。格式不对的项直接跳过，不抛异常。
    /// </summary>
    /// <param name="header">Server-Timing 头的值；可以为 null。</param>
    /// <returns>阶段名 → 毫秒；没有头时为空。</returns>
    public static Dictionary<string, double> Parse(string? header)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (header == null || header.Length == 0)
        {
            return result;
        }

        foreach (var entry in header.Split(','))
        {
            var parts = entry.Split(';');
            var name = parts[0].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            for (var i = 1; i < parts.Length; i++)
            {
                var parameter = parts[i].Trim();
                if (parameter.StartsWith("dur=", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(parameter.Substring(4), NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
                {
                    result[name] = milliseconds;
                }
            }
        }

        return result;
    }
}

/// <summary>
/// 服务端的错误响应体。
/// </summary>
internal sealed class ErrorBody
{
    /// <summary>
    /// 错误码，如 FIELD_REQUIRED。
    /// </summary>
    public string? Code
    {
        get; set;
    }

    /// <summary>
    /// 给人看的错误说明。
    /// </summary>
    public string? Message
    {
        get; set;
    }

    /// <summary>
    /// 逐个字段的错误；没有时为 null。
    /// </summary>
    public List<LabelFieldError>? Errors
    {
        get; set;
    }

    /// <summary>
    /// 服务端的跟踪 ID。
    /// </summary>
    public string? TraceId
    {
        get; set;
    }
}
