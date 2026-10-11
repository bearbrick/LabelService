using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Templates;

namespace LabelService.Application.Templates;

/// <summary>
/// 模板内容（模板 JSON）和库里存的字符串之间的转换，以及模板库列表用的轮廓。
/// </summary>
public static class TemplateContent
{
    /// <summary>
    /// 把模板写成库里存的 JSON（和开放接口同一套序列化选项）。
    /// </summary>
    /// <param name="template">模板内容。</param>
    /// <returns>JSON 字符串。</returns>
    public static string Serialize(LabelTemplate template) => JsonSerializer.Serialize(template, LabelJson.Options);

    /// <summary>
    /// 读出库里存的模板 JSON。
    /// </summary>
    /// <param name="json">JSON 字符串。</param>
    /// <returns>模板内容。</returns>
    /// <exception cref="JsonException">JSON 不合法或为空（库被改坏了）。</exception>
    public static LabelTemplate Deserialize(string json) =>
        JsonSerializer.Deserialize<LabelTemplate>(json, LabelJson.Options) ?? throw new JsonException("模板内容为空");

    /// <summary>
    /// 只读模板 JSON 里的画布和字段、元素个数，不做完整的反序列化（模板库列表一次要读全部模板）。
    /// </summary>
    /// <param name="json">模板 JSON；从未保存过内容时为 null。</param>
    /// <returns>轮廓；读不到的项为 0。</returns>
    public static TemplateOutline Outline(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new TemplateOutline(0, 0, 0, 0, 0);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var fields = CountArray(root, "fields");
        var elements = CountArray(root, "elements");
        return root.TryGetProperty("page", out var page) && page.ValueKind == JsonValueKind.Object
            ? new TemplateOutline(ReadNumber(page, "width"), ReadNumber(page, "height"), (int)ReadNumber(page, "dpi"), fields, elements)
            : new TemplateOutline(0, 0, 0, fields, elements);
    }

    /// <summary>
    /// 读一个数字属性。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>属性值；没有或不是数字时为 0。</returns>
    private static double ReadNumber(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;

    /// <summary>
    /// 数一个数组属性的长度。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>数组长度；没有或不是数组时为 0。</returns>
    private static int CountArray(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
}

/// <summary>
/// 模板当前内容（草稿或最新发布版本）的轮廓。
/// </summary>
/// <param name="Width">画布宽度，mm。</param>
/// <param name="Height">画布高度，mm。</param>
/// <param name="Dpi">分辨率。</param>
/// <param name="FieldCount">字段个数。</param>
/// <param name="ElementCount">元素个数。</param>
public sealed record TemplateOutline(double Width, double Height, int Dpi, int FieldCount, int ElementCount);
