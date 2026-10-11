using System.Globalization;
using System.Text.Json;

namespace LabelService.Client.Internal;

/// <summary>
/// 请求的本地校验和请求体生成。校验不通过时抛 ArgumentException，不发请求。
/// </summary>
internal static class Payloads
{
    /// <summary>
    /// 份数下限（和服务端一致）。
    /// </summary>
    private const int MinCopies = 1;

    /// <summary>
    /// 份数上限（和服务端一致）。
    /// </summary>
    private const int MaxCopies = 100;

    /// <summary>
    /// 校验单张请求并生成请求体。
    /// </summary>
    /// <param name="request">单张请求。</param>
    /// <returns>UTF-8 JSON 请求体。</returns>
    /// <exception cref="ArgumentException">模板编码为空、份数越界、PNG 多份（不拼版时）或拼版参数不对。</exception>
    public static byte[] Build(RenderRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        RequireTemplateCode(request.TemplateCode);
        RequireCopies(request.Copies, "Copies");
        var sheet = Sheet(request.Layout, request.StartSlot, request.Format);
        if (request.Format == LabelFormat.Png && request.Copies != 1 && !sheet)
        {
            throw new ArgumentException("PNG 单张只能 1 份；多份请用 RenderBatchAsync 并选 Zip，或者用拼版版式。", nameof(request));
        }

        var body = Common(request.TemplateCode, request.Version, request.Format, request.Dpi, request.Mono);
        AddSheet(body, request.Layout, request.StartSlot);
        body["copies"] = request.Copies;
        body["data"] = Values(request.Data);
        return JsonSerializer.SerializeToUtf8Bytes(body, ClientJson.Options);
    }

    /// <summary>
    /// 校验批量请求并生成请求体。
    /// </summary>
    /// <param name="request">批量请求。</param>
    /// <param name="maxLabels">单次总张数上限（份数加起来）。</param>
    /// <returns>UTF-8 JSON 请求体。</returns>
    /// <exception cref="ArgumentException">模板编码为空、没有条目、份数越界、总张数超过上限、PNG 没选 Zip（不拼版时）或拼版参数不对。</exception>
    public static byte[] Build(BatchRenderRequest request, int maxLabels)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        RequireTemplateCode(request.TemplateCode);
        if (request.Items.Count == 0)
        {
            throw new ArgumentException("批量请求至少要有一条。", nameof(request));
        }

        var sheet = Sheet(request.Layout, request.StartSlot, request.Format);
        if (request.Format == LabelFormat.Png && request.Output != BatchOutput.Zip && !sheet)
        {
            throw new ArgumentException("PNG 批量只能用 BatchOutput.Zip。", nameof(request));
        }

        var total = 0;
        var items = new List<Dictionary<string, object?>>(request.Items.Count);
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            RequireCopies(item.Copies, $"Items[{i}].Copies");
            total += item.Copies;
            items.Add(new Dictionary<string, object?> { ["data"] = Values(item.Data), ["copies"] = item.Copies });
        }

        if (total > maxLabels)
        {
            throw new ArgumentException($"总张数 {total} 超过单次上限 {maxLabels}，请拆批。", nameof(request));
        }

        var body = Common(request.TemplateCode, request.Version, request.Format, request.Dpi, request.Mono);
        AddSheet(body, request.Layout, request.StartSlot);
        body["output"] = request.Output == BatchOutput.Zip ? "zip" : "merge";
        body["common"] = Values(request.Common);
        body["items"] = items;
        return JsonSerializer.SerializeToUtf8Bytes(body, ClientJson.Options);
    }

    /// <summary>
    /// 输出格式在接口里的写法。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <returns>pdf、png 或 zpl。</returns>
    public static string FormatName(LabelFormat format)
    {
        switch (format)
        {
            case LabelFormat.Pdf:
                return "pdf";
            case LabelFormat.Png:
                return "png";
            case LabelFormat.Zpl:
                return "zpl";
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    /// <summary>
    /// 把调用方的值转成 JSON 友好的形式：日期转 ISO 8601，byte[] 转 base64，枚举转字符串，null 不发送。
    /// </summary>
    /// <param name="source">字段 key → 调用方传的值。</param>
    /// <returns>字段 key → 可以直接序列化的值（去掉了 null）。</returns>
    public static Dictionary<string, object?> Values(IDictionary<string, object?> source)
    {
        var result = new Dictionary<string, object?>(source.Count, StringComparer.Ordinal);
        foreach (var pair in source)
        {
            var value = ConvertValue(pair.Value);
            if (value != null)
            {
                result[pair.Key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// 把一个值转成可以直接序列化的形式：基本类型和 JsonElement 原样，日期转 ISO 8601，byte[] 转 base64，其他转字符串。
    /// </summary>
    /// <param name="value">调用方传的值。</param>
    /// <returns>转换后的值；null 仍是 null。</returns>
    public static object? ConvertValue(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string _:
            case bool _:
            case byte _:
            case short _:
            case int _:
            case long _:
            case float _:
            case double _:
            case decimal _:
            case JsonElement _:
                return value;
            case DateTime date:
                return date.TimeOfDay == TimeSpan.Zero
                    ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            case DateTimeOffset offset:
                return offset.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
            case byte[] bytes:
                return Convert.ToBase64String(bytes);
            case Enum enumValue:
                return enumValue.ToString();
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 单张和批量共用的请求体属性：模板编码、格式，以及指定了的版本、DPI、黑白。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="version">模板版本；null 表示最新发布版。</param>
    /// <param name="format">输出格式。</param>
    /// <param name="dpi">分辨率；null 表示用模板的。</param>
    /// <param name="mono">是否黑白输出。</param>
    /// <returns>请求体（属性名 → 值），后面再补其他属性。</returns>
    private static Dictionary<string, object?> Common(string templateCode, int? version, LabelFormat format, int? dpi, bool mono)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["templateCode"] = templateCode,
            ["format"] = FormatName(format),
        };
        if (version != null)
        {
            body["version"] = version.Value;
        }

        if (dpi != null)
        {
            body["dpi"] = dpi.Value;
        }

        if (mono)
        {
            body["mono"] = true;
        }

        return body;
    }

    /// <summary>
    /// 拼版参数的本地校验：只支持 PDF 和 PNG，StartSlot 从 1 开始（上限要看版式，由服务端检查）。返回是否用了版式。
    /// </summary>
    /// <param name="layout">版式编码；null 或空表示不拼版。</param>
    /// <param name="startSlot">起始格子；null 表示从第 1 格开始。</param>
    /// <param name="format">输出格式。</param>
    /// <returns>用了版式时为 true。</returns>
    /// <exception cref="ArgumentException">ZPL 用了版式，或 StartSlot 小于 1。</exception>
    private static bool Sheet(string? layout, int? startSlot, LabelFormat format)
    {
        if (layout == null || layout.Length == 0)
        {
            return false;
        }

        if (format == LabelFormat.Zpl)
        {
            throw new ArgumentException("ZPL 不支持拼版，Layout 只能和 PDF、PNG 一起用。", "Format");
        }

        if (startSlot != null && startSlot.Value < 1)
        {
            throw new ArgumentException("StartSlot 从 1 开始。", "StartSlot");
        }

        return true;
    }

    /// <summary>
    /// 用了版式时把 layout 和 startSlot 写进请求体。
    /// </summary>
    /// <param name="body">请求体。</param>
    /// <param name="layout">版式编码；null 或空时什么也不写。</param>
    /// <param name="startSlot">起始格子；null 时不写。</param>
    private static void AddSheet(Dictionary<string, object?> body, string? layout, int? startSlot)
    {
        if (layout == null || layout.Length == 0)
        {
            return;
        }

        body["layout"] = layout;
        if (startSlot != null)
        {
            body["startSlot"] = startSlot.Value;
        }
    }

    /// <summary>
    /// 模板编码不能为空。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <exception cref="ArgumentException">为空或只有空白。</exception>
    private static void RequireTemplateCode(string? templateCode)
    {
        if (templateCode == null || templateCode.Trim().Length == 0)
        {
            throw new ArgumentException("模板编码不能为空。", nameof(templateCode));
        }
    }

    /// <summary>
    /// 份数要在 1–100。
    /// </summary>
    /// <param name="copies">份数。</param>
    /// <param name="name">报错时显示的参数名，如 Items[3].Copies。</param>
    /// <exception cref="ArgumentException">份数越界。</exception>
    private static void RequireCopies(int copies, string name)
    {
        if (copies < MinCopies || copies > MaxCopies)
        {
            throw new ArgumentException($"{name} 应在 {MinCopies}–{MaxCopies}，实际为 {copies}。", name);
        }
    }
}
