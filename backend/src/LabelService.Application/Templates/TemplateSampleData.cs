using System.Text.Json;
using LabelService.Contracts.Templates;

namespace LabelService.Application.Templates;

/// <summary>
/// 用字段的样例值拼一份数据：发布检查、缩略图、服务端预览、拼版预览、启动预热都用它渲染。
/// </summary>
public static class TemplateSampleData
{
    /// <summary>
    /// 图片字段没有样例值时用的占位图：1×1 透明 PNG。
    /// </summary>
    private const string PlaceholderImage = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    /// <summary>
    /// 样例数据：用字段的 sample；必填、没有样例也没有默认值的字段用占位值，并提示补样例。
    /// </summary>
    /// <param name="fields">模板的字段定义。</param>
    /// <param name="warnings">用了占位值的字段在这里追加一条提示；不需要提示时传一个丢弃的列表。</param>
    /// <returns>字段 key → 值。</returns>
    public static Dictionary<string, JsonElement> Create(IReadOnlyList<FieldDefinition> fields, List<string> warnings)
    {
        var data = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            // 有自动值的字段不给：试渲染和预览显示自动值算出来的样子（和实际打出来的一样）。
            if (field.Source == FieldSource.Template || field.Auto is not null)
            {
                continue;
            }

            // 有样例值就用样例值。
            if (field.Sample is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } sample)
            {
                data[field.Key] = sample;
                continue;
            }

            // 选填、或者有默认值的字段不用给：渲染时按选填或默认值处理。
            if (!field.Required || field.Default is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) })
            {
                continue;
            }

            warnings.Add($"字段 {field.Key} 没有样例值，试渲染用了占位值；建议补上样例，方便调用方对照");
            data[field.Key] = field.Type switch
            {
                FieldType.Number => JsonSerializer.SerializeToElement(0),
                FieldType.Date => JsonSerializer.SerializeToElement("2026-01-01"),
                FieldType.Image => JsonSerializer.SerializeToElement(PlaceholderImage),
                _ => JsonSerializer.SerializeToElement(field.Key),
            };
        }

        return data;
    }
}
