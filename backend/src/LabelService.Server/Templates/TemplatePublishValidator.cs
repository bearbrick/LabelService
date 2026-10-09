using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Binding;
using LabelService.Rendering.Validation;

namespace LabelService.Server.Templates;

/// <summary>
/// 发布前检查模板：静态规则 + 用样例值试渲染一次并计时（设计书第 9 节、T-008）。
/// </summary>
/// <remarks>
/// 静态规则和设计器导出前的检查（frontend/src/model/validate.ts）一致，另加条码旋转必须是直角。
/// 只在发布时执行，不在开放接口的请求路径上。
/// </remarks>
internal sealed partial class TemplatePublishValidator(ILabelRenderer renderer)
{
    /// <summary>
    /// 试渲染超过这个时间给提示，不阻止发布。
    /// </summary>
    public const double SlowRenderMs = 50;

    /// <summary>
    /// 图片字段没有样例值时试渲染用的占位图：1×1 透明 PNG。
    /// </summary>
    private const string PlaceholderImage = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    /// <summary>
    /// 模板编码规则：字母开头，只含字母、数字、下划线、连字符，最长 64。编码会出现在接口路径和文件名里。
    /// </summary>
    public static bool IsValidCode(string? code) => code is not null && CodePattern().IsMatch(code);

    /// <summary>
    /// 检查一份模板。有错误时不试渲染。
    /// </summary>
    public PublishCheck Check(LabelTemplate template)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        CheckStatic(template, errors, warnings);
        if (errors.Count > 0)
        {
            return new PublishCheck(errors, warnings, null);
        }

        var sample = SampleData(template.Fields, warnings);
        var validation = FieldValidator.Validate(template.Fields, null, sample);
        if (!validation.IsValid)
        {
            errors.AddRange(validation.Errors.Select(error => "样例值不合法：" + (error.Message ?? error.Code)));
            return new PublishCheck(errors, warnings, null);
        }

        var job = new RenderJob
        {
            Template = template,
            Labels = [new LabelInstance(validation.Values)],
            Format = LabelFormat.Png,
            Dpi = template.Page.Dpi > 0 ? template.Page.Dpi : RenderLimits.DefaultDpi,
        };
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = renderer.Render(job);
            warnings.AddRange(result.Warnings.Select(warning => "试渲染警告：" + Uri.UnescapeDataString(warning)));
        }
        catch (LabelRenderException ex)
        {
            errors.Add($"试渲染失败（{ex.ErrorCode}）：{ex.Message}");
            return new PublishCheck(errors, warnings, null);
        }

        var renderMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (renderMs > SlowRenderMs)
        {
            warnings.Add($"试渲染耗时 {renderMs:0.0} ms，超过 {SlowRenderMs:0} ms，接口调用可能变慢；可以减少元素或缩小图片");
        }

        return new PublishCheck(errors, warnings, renderMs);
    }

    private static void CheckStatic(LabelTemplate template, List<string> errors, List<string> warnings)
    {
        if (!IsValidCode(template.Code))
        {
            errors.Add($"模板编码“{template.Code}”不合法：要以字母开头，只含字母、数字、下划线、连字符，最长 64 个字符");
        }

        if (!(template.Page.Width > 0) || !(template.Page.Height > 0))
        {
            errors.Add("画布宽高必须大于 0");
        }

        if (template.Page.Dpi > 0 && !RenderLimits.AllowedDpi.Contains(template.Page.Dpi))
        {
            errors.Add($"画布 DPI 只能是 {string.Join("、", RenderLimits.AllowedDpi)}");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in template.Fields)
        {
            if (!FieldKeyPattern().IsMatch(field.Key))
            {
                errors.Add($"字段 key“{field.Key}”不合法：要以字母开头，只含字母、数字、下划线");
            }
            else if (!keys.Add(field.Key))
            {
                errors.Add($"字段 key“{field.Key}”重复");
            }
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in template.Elements)
        {
            var label = string.IsNullOrEmpty(element.Name) ? element.Id : $"{element.Name}（{element.Id}）";
            if (string.IsNullOrEmpty(element.Id))
            {
                errors.Add($"有元素缺少 ID（{element.Type}）");
            }
            else if (!ids.Add(element.Id))
            {
                errors.Add($"元素 ID“{element.Id}”重复");
            }

            foreach (var key in BoundText(element).SelectMany(BindingResolver.FindKeys))
            {
                used.Add(key);
                if (!keys.Contains(key))
                {
                    errors.Add($"元素 {label} 引用了未定义的字段 {key}");
                }
            }

            if (element is BarcodeElement && !IsRightAngle(element.Rotation))
            {
                errors.Add($"一维码 {label} 的旋转角度是 {element.Rotation}°，只能是 0、90、180、270，否则扫不出来");
            }
        }

        warnings.AddRange(keys.Where(key => !used.Contains(key)).Select(key => $"字段 {key} 没有被任何元素引用"));
    }

    private static IEnumerable<string> BoundText(LabelElement element) => element switch
    {
        TextElement text => [text.Content],
        BarcodeElement barcode => [barcode.Value],
        QrCodeElement qrcode => [qrcode.Value],
        ImageElement image => [image.Src],
        _ => [],
    };

    private static bool IsRightAngle(double rotation)
    {
        var remainder = Math.Abs(rotation % 90);
        return remainder < 1e-6 || 90 - remainder < 1e-6;
    }

    /// <summary>
    /// 样例值：用字段的 sample；必填、没有样例也没有默认值的字段用占位值，并提示补样例。
    /// </summary>
    internal static Dictionary<string, JsonElement> SampleData(IReadOnlyList<FieldDefinition> fields, List<string> warnings)
    {
        var data = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (field.Sample is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } sample)
            {
                data[field.Key] = sample;
                continue;
            }

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

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldKeyPattern();
}

/// <summary>
/// 发布检查的结果。
/// </summary>
/// <param name="Errors">阻止发布的问题。</param>
/// <param name="Warnings">提示，不阻止发布。</param>
/// <param name="RenderMs">试渲染耗时；有错误、没走到试渲染时为 null。</param>
internal sealed record PublishCheck(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, double? RenderMs)
{
    /// <summary>可以发布。</summary>
    public bool CanPublish => Errors.Count == 0;
}
