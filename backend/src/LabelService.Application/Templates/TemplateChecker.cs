using System.Diagnostics;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Domain.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Binding;
using LabelService.Rendering.Validation;

namespace LabelService.Application.Templates;

/// <summary>
/// 发布前检查模板：静态规则 + 用样例值试渲染一次并计时（设计书第 9 节）。
/// </summary>
/// <remarks>
/// 静态规则和设计器导出前的检查（frontend/src/model/validate.ts）一致，另加条码旋转必须是直角。
/// 只在发布、检查按钮、服务端预览时执行，不在开放接口的请求路径上。
/// </remarks>
/// <param name="renderer">渲染引擎，试渲染用。</param>
public sealed class TemplateChecker(ILabelRenderer renderer)
{
    /// <summary>
    /// 试渲染超过这个时间给提示，不阻止发布。
    /// </summary>
    public const double SlowRenderMs = 50;

    /// <summary>
    /// 检查一份模板：先静态规则，没有错误再用样例值试渲染。
    /// </summary>
    /// <param name="template">模板内容。</param>
    /// <returns>错误、提示和试渲染耗时。</returns>
    public TemplateCheckResultDto Check(LabelTemplate template)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        CheckStatic(template, errors, warnings);
        if (errors.Count > 0)
        {
            return new TemplateCheckResultDto(errors, warnings, null);
        }

        // 样例值本身也要合法，否则调用方照着样例调也会失败。
        var sample = TemplateSampleData.Create(template.Fields, warnings);
        var validation = FieldValidator.Validate(template.Fields, null, sample);
        if (!validation.IsValid)
        {
            errors.AddRange(validation.Errors.Select(error => "样例值不合法：" + (error.Message ?? error.Code)));
            return new TemplateCheckResultDto(errors, warnings, null);
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
            return new TemplateCheckResultDto(errors, warnings, null);
        }

        var renderMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (renderMs > SlowRenderMs)
        {
            warnings.Add($"试渲染耗时 {renderMs:0.0} ms，超过 {SlowRenderMs:0} ms，接口调用可能变慢；可以减少元素或缩小图片");
        }

        return new TemplateCheckResultDto(errors, warnings, renderMs);
    }

    /// <summary>
    /// 服务端预览前的检查：和发布一样的静态规则，但不要求模板编码合法（未保存的模板也能预览）。
    /// </summary>
    /// <param name="template">模板内容。</param>
    /// <returns>全部错误；没有时为空列表。</returns>
    public static IReadOnlyList<string> PreviewErrors(LabelTemplate template)
    {
        var errors = new List<string>();
        CheckStatic(template, errors, [], checkCode: false);
        return errors;
    }

    /// <summary>
    /// 静态规则：编码、画布、DPI、字段 key、元素 ID、绑定的字段是否定义、条码旋转角度。
    /// </summary>
    /// <param name="template">模板内容。</param>
    /// <param name="errors">阻止发布的问题追加到这里。</param>
    /// <param name="warnings">提示追加到这里（如字段没有被引用）。</param>
    /// <param name="checkCode">是否检查模板编码。</param>
    private static void CheckStatic(LabelTemplate template, List<string> errors, List<string> warnings, bool checkCode = true)
    {
        if (checkCode && !TemplateCodes.IsValidCode(template.Code))
        {
            errors.Add($"模板编码“{template.Code}”不合法：{TemplateCodes.CodeRule}");
        }

        if (!(template.Page.Width > 0) || !(template.Page.Height > 0))
        {
            errors.Add("画布宽高必须大于 0");
        }

        if (template.Page.Dpi > 0 && !RenderLimits.AllowedDpi.Contains(template.Page.Dpi))
        {
            errors.Add($"画布 DPI 只能是 {string.Join("、", RenderLimits.AllowedDpi)}");
        }

        // 字段 key：格式合法、不重复。
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in template.Fields)
        {
            if (!TemplateCodes.IsValidFieldKey(field.Key))
            {
                errors.Add($"字段 key“{field.Key}”不合法：要以字母开头，只含字母、数字、下划线");
            }
            else if (!keys.Add(field.Key))
            {
                errors.Add($"字段 key“{field.Key}”重复");
            }
        }

        // 元素：ID 必填且不重复，绑定的字段都要定义，一维码只能转直角。
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

    /// <summary>
    /// 元素里可能含绑定（{{key}}）的文字。
    /// </summary>
    /// <param name="element">元素。</param>
    /// <returns>文本内容、条码值、二维码值、图片来源；其他元素没有。</returns>
    private static IEnumerable<string> BoundText(LabelElement element) => element switch
    {
        TextElement text => [text.Content],
        BarcodeElement barcode => [barcode.Value],
        QrCodeElement qrcode => [qrcode.Value],
        ImageElement image => [image.Src],
        _ => [],
    };

    /// <summary>
    /// 旋转角度是不是 90 的整数倍（允许浮点误差）。
    /// </summary>
    /// <param name="rotation">角度。</param>
    /// <returns>是直角时为 true。</returns>
    private static bool IsRightAngle(double rotation)
    {
        var remainder = Math.Abs(rotation % 90);
        return remainder < 1e-6 || 90 - remainder < 1e-6;
    }
}

/// <summary>
/// 发布检查的结果（接口 POST /api/admin/templates/check 原样返回）。
/// </summary>
/// <param name="Errors">阻止发布的问题。</param>
/// <param name="Warnings">提示，不阻止发布。</param>
/// <param name="RenderMs">试渲染耗时，毫秒；有错误、没走到试渲染时为 null。</param>
public sealed record TemplateCheckResultDto(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, double? RenderMs)
{
    /// <summary>
    /// 能不能发布：没有错误。
    /// </summary>
    public bool CanPublish => Errors.Count == 0;
}
