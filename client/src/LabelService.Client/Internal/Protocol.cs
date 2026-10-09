using System.Globalization;
using System.Text.Json;

namespace LabelService.Client.Internal;

/// <summary>
/// HTTP 头名称。是 LabelService.Contracts.Protocol.HeaderNames 的副本，检查程序核对两边一致。
/// </summary>
internal static class HeaderNames
{
    public const string ApiKey = "X-Api-Key";
    public const string RequestId = "X-Request-Id";
    public const string TemplateVersion = "X-Template-Version";
    public const string LabelCount = "X-Label-Count";
    public const string LabelDpi = "X-Label-Dpi";
    public const string TraceId = "X-Trace-Id";
    public const string ServerTiming = "Server-Timing";
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
/// 序列化设置。
/// </summary>
internal static class ClientJson
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };
}

/// <summary>
/// 服务端的错误响应体。
/// </summary>
internal sealed class ErrorBody
{
    public string? Code
    {
        get; set;
    }

    public string? Message
    {
        get; set;
    }

    public List<LabelFieldError>? Errors
    {
        get; set;
    }

    public string? TraceId
    {
        get; set;
    }
}
