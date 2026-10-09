using System.Globalization;
using System.Text.Json;

namespace LabelService.Client.Internal;

/// <summary>
/// 请求的本地校验和请求体生成。校验不通过时抛 ArgumentException，不发请求。
/// </summary>
internal static class Payloads
{
    private const int MinCopies = 1;
    private const int MaxCopies = 100;

    public static byte[] Build(RenderRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        RequireTemplateCode(request.TemplateCode);
        RequireCopies(request.Copies, "Copies");
        if (request.Format == LabelFormat.Png && request.Copies != 1)
        {
            throw new ArgumentException("PNG 单张只能 1 份；多份请用 RenderBatchAsync 并选 Zip。", nameof(request));
        }

        var body = Common(request.TemplateCode, request.Version, request.Format, request.Dpi, request.Mono);
        body["copies"] = request.Copies;
        body["data"] = Values(request.Data);
        return JsonSerializer.SerializeToUtf8Bytes(body, ClientJson.Options);
    }

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

        if (request.Format == LabelFormat.Png && request.Output != BatchOutput.Zip)
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
        body["output"] = request.Output == BatchOutput.Zip ? "zip" : "merge";
        body["common"] = Values(request.Common);
        body["items"] = items;
        return JsonSerializer.SerializeToUtf8Bytes(body, ClientJson.Options);
    }

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

    private static void RequireTemplateCode(string? templateCode)
    {
        if (templateCode == null || templateCode.Trim().Length == 0)
        {
            throw new ArgumentException("模板编码不能为空。", nameof(templateCode));
        }
    }

    private static void RequireCopies(int copies, string name)
    {
        if (copies < MinCopies || copies > MaxCopies)
        {
            throw new ArgumentException($"{name} 应在 {MinCopies}–{MaxCopies}，实际为 {copies}。", name);
        }
    }
}
