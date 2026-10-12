using System.Globalization;
using LabelService.Contracts.Templates;

namespace LabelService.Rendering.Validation;

/// <summary>
/// 字段的自动值（<see cref="FieldDefinition.Auto"/>，ADR 0012）：调用方没传时由服务端算出来。
/// </summary>
/// <remarks>
/// 只认固定的几种写法，和字段类型对应：date 用 today、now、today±N(d|m|y)；number 用 index、total；string 用 client、requestId。
/// </remarks>
public static class AutoValues
{
    /// <summary>
    /// 当前日期和时间（到秒）。
    /// </summary>
    public const string Now = "now";

    /// <summary>
    /// 当前日期。
    /// </summary>
    public const string Today = "today";

    /// <summary>
    /// 这一张在本次请求里是第几条，从 1 开始。
    /// </summary>
    public const string Index = "index";

    /// <summary>
    /// 本次请求一共几条。
    /// </summary>
    public const string Total = "total";

    /// <summary>
    /// 调用方名称。
    /// </summary>
    public const string Client = "client";

    /// <summary>
    /// 这次调用的请求号（X-Request-Id）。
    /// </summary>
    public const string RequestId = "requestId";

    /// <summary>
    /// 相对日期里 N 的上限。
    /// </summary>
    private const int MaxOffset = 9999;

    /// <summary>
    /// 自动值的写法和字段类型是否匹配。
    /// </summary>
    /// <param name="type">字段类型。</param>
    /// <param name="auto">自动值写法。</param>
    /// <returns>认识且类型匹配时为 true。</returns>
    public static bool IsValid(FieldType type, string auto) => type switch
    {
        FieldType.Date => auto is Now or Today || TryParseRelative(auto, out _, out _),
        FieldType.Number => auto is Index or Total,
        FieldType.String => auto is Client or RequestId,
        _ => false,
    };

    /// <summary>
    /// 算出自动值：date 是 <see cref="DateTime"/>，number 是 decimal，string 是 string；和调用方传值转换后的类型一样。
    /// </summary>
    /// <param name="type">字段类型。</param>
    /// <param name="auto">自动值写法。</param>
    /// <param name="context">这次请求的当前时间、总条数、调用方和请求号。</param>
    /// <param name="index">批量条目序号（从 0 开始）；单张为 null。</param>
    /// <param name="value">算出的值。</param>
    /// <returns>写法认识且类型匹配时为 true；否则当作没有自动值。</returns>
    public static bool TryResolve(FieldType type, string auto, AutoValueContext context, int? index, out object? value)
    {
        value = null;
        if (!IsValid(type, auto))
        {
            return false;
        }

        var today = context.Now.Date;
        value = auto switch
        {
            Now => new DateTime(context.Now.Ticks - (context.Now.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Unspecified),
            Today => today,
            Index => (decimal)((index ?? 0) + 1),
            Total => (decimal)context.Total,
            Client => context.ClientName,
            RequestId => context.RequestId,
            _ => Relative(today, auto),
        };
        return true;
    }

    /// <summary>
    /// 今天加减 N 天、月、年；加月、加年遇到月底取当月最后一天（1 月 31 日加 1 个月是 2 月 28 日或 29 日）。
    /// </summary>
    /// <param name="today">今天。</param>
    /// <param name="auto">today+30d 这样的写法（已经校验过）。</param>
    /// <returns>算出的日期。</returns>
    private static DateTime Relative(DateTime today, string auto)
    {
        TryParseRelative(auto, out var offset, out var unit);
        return unit switch
        {
            'd' => today.AddDays(offset),
            'm' => today.AddMonths(offset),
            _ => today.AddYears(offset),
        };
    }

    /// <summary>
    /// 解析 today+30d、today-1d、today+6m、today+1y。
    /// </summary>
    /// <param name="auto">写法。</param>
    /// <param name="offset">带正负号的 N。</param>
    /// <param name="unit">单位：d 天、m 月、y 年。</param>
    /// <returns>写法正确时为 true。</returns>
    private static bool TryParseRelative(string auto, out int offset, out char unit)
    {
        offset = 0;
        unit = auto.Length > 0 ? auto[^1] : '\0';
        // 形如 today + 符号 + 1～4 位数字 + 单位
        if (auto.Length < Today.Length + 3 || !auto.StartsWith(Today, StringComparison.Ordinal) || unit is not ('d' or 'm' or 'y'))
        {
            return false;
        }

        var sign = auto[Today.Length];
        var digits = auto.AsSpan(Today.Length + 1, auto.Length - Today.Length - 2);
        if (sign is not ('+' or '-') || digits.Length is 0 or > 4 || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > MaxOffset)
        {
            return false;
        }

        offset = sign == '+' ? number : -number;
        return true;
    }
}

/// <summary>
/// 算自动值用到的这次请求的信息：同一次请求只取一次当前时间，批量里每张都一样。
/// </summary>
public sealed class AutoValueContext
{
    /// <summary>
    /// 当前时间，已经换算到服务端配置的时区（钟面时间，Kind 为 Unspecified）。
    /// </summary>
    public DateTime Now
    {
        get; init;
    }

    /// <summary>
    /// 本次请求一共几条；单张为 1。
    /// </summary>
    public int Total { get; init; } = 1;

    /// <summary>
    /// 调用方名称。
    /// </summary>
    public string ClientName { get; init; } = "";

    /// <summary>
    /// 这次调用的请求号。
    /// </summary>
    public string RequestId { get; init; } = "";

    /// <summary>
    /// 预览、发布前试渲染、启动预热用：服务器本机的当前时间，调用方和请求号用示意文字。
    /// </summary>
    /// <returns>新的上下文。</returns>
    public static AutoValueContext ForPreview() => new()
    {
        Now = DateTime.Now,
        ClientName = "示例调用方",
        RequestId = "PREVIEW",
    };
}
