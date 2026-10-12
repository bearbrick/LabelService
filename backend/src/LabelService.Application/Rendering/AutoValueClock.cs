using LabelService.Contracts.Templates;
using LabelService.Rendering.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelService.Application.Rendering;

/// <summary>
/// 字段自动值（ADR 0012）用的当前时间和请求信息：时间按配置的时区（LabelService:Render:TimeZone）换算，不配时用服务器本机时区。
/// </summary>
public sealed class AutoValueClock
{
    /// <summary>
    /// 时间来源（检查程序里可以换成固定时间）。
    /// </summary>
    private readonly TimeProvider time;

    /// <summary>
    /// 自动值用的时区。
    /// </summary>
    private readonly TimeZoneInfo zone;

    /// <summary>
    /// 读取时区配置；配置的时区不存在时记警告，改用本机时区。
    /// </summary>
    /// <param name="time">时间来源。</param>
    /// <param name="options">渲染配置。</param>
    /// <param name="logger">日志。</param>
    public AutoValueClock(TimeProvider time, IOptions<RenderOptions> options, ILogger<AutoValueClock> logger)
    {
        this.time = time;
        zone = ResolveZone(options.Value.TimeZone, logger);
    }

    /// <summary>
    /// 自动值用的时区。
    /// </summary>
    public TimeZoneInfo TimeZone => zone;

    /// <summary>
    /// 这个时区的当前钟面时间。
    /// </summary>
    /// <returns>当前时间（Kind 为 Unspecified）。</returns>
    public DateTime Now() => TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime;

    /// <summary>
    /// 一次请求的自动值上下文；模板没有带自动值的字段时为 null（热路径上不多做事）。
    /// </summary>
    /// <param name="fields">模板的字段。</param>
    /// <param name="total">本次请求一共几条。</param>
    /// <param name="clientName">调用方名称。</param>
    /// <param name="requestId">请求号。</param>
    /// <returns>上下文；不需要时为 null。</returns>
    public AutoValueContext? For(IReadOnlyList<FieldDefinition> fields, int total, string clientName, string requestId)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (fields[i].Auto is not null)
            {
                return new AutoValueContext { Now = Now(), Total = total, ClientName = clientName, RequestId = requestId };
            }
        }

        return null;
    }

    /// <summary>
    /// 按 ID 找时区（IANA 的 Asia/Shanghai 和 Windows 的 China Standard Time 都认）；空表示本机时区。
    /// </summary>
    /// <param name="id">时区 ID。</param>
    /// <param name="logger">日志。</param>
    /// <returns>时区。</returns>
    private static TimeZoneInfo ResolveZone(string? id, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning("LabelService:Render:TimeZone 配置的时区 {TimeZone} 不存在，自动值改用服务器本机时区 {Local}", id, TimeZoneInfo.Local.Id);
            return TimeZoneInfo.Local;
        }
    }
}
