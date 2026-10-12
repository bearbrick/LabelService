using System.Diagnostics;
using LabelService.Application.Diagnostics;
using LabelService.Application.Rendering;
using LabelService.Application.Rendering.Dtos;
using LabelService.Application.Templates.Dtos;
using LabelService.Contracts.Protocol;
using LabelService.Domain.Shared;
using LabelService.Rendering;
using LabelService.Rendering.Validation;

namespace LabelService.Application.Templates;

/// <summary>
/// 服务端预览的应用服务实现。
/// </summary>
/// <param name="renderer">渲染引擎。</param>
/// <param name="metrics">指标（endpoint=preview）。</param>
/// <param name="clock">字段自动值用的当前时间。</param>
public sealed class TemplatePreviewAppService(ILabelRenderer renderer, LabelMetrics metrics, AutoValueClock clock) : ITemplatePreviewAppService
{
    /// <inheritdoc />
    public RenderedLabelDto Render(PreviewInput input)
    {
        var started = Stopwatch.GetTimestamp();
        var template = input.Template ?? throw new UserFriendlyException("请求体缺少 template（模板 JSON）");
        var errors = TemplateChecker.PreviewErrors(template);
        if (errors.Count > 0)
        {
            throw new UserFriendlyException("模板有问题，不能预览：" + errors[0], errors);
        }

        var dpi = input.Dpi ?? (template.Page.Dpi > 0 ? template.Page.Dpi : RenderLimits.DefaultDpi);
        if (!RenderLimits.AllowedDpi.Contains(dpi))
        {
            throw new UserFriendlyException($"dpi 只能是 {string.Join("、", RenderLimits.AllowedDpi)}");
        }

        // 没传数据时用字段的样例值，和发布检查的试渲染一致。
        var data = input.Data ?? TemplateSampleData.Create(template.Fields, []);
        var validation = FieldValidator.Validate(template.Fields, null, data, null, clock.For(template.Fields, 1, "示例调用方", "PREVIEW"));
        if (!validation.IsValid)
        {
            throw new UserFriendlyException(
                "数据不合法：" + (validation.Errors[0].Message ?? validation.Errors[0].Code),
                validation.Errors.Select(error => $"{error.Code}：{error.Message}").ToArray());
        }

        RenderResult result;
        try
        {
            result = renderer.Render(new RenderJob
            {
                Template = template,
                Labels = [new LabelInstance(validation.Values)],
                Format = LabelFormat.Png,
                Dpi = dpi,
                Mono = input.Mono ?? false,
            });
        }
        catch (LabelRenderException ex)
        {
            metrics.PreviewFinished(template.Code ?? "", dpi, ex.ErrorCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw new UserFriendlyException($"渲染失败（{ex.ErrorCode}）：{ex.Message}");
        }

        metrics.PreviewFinished(template.Code ?? "", dpi, "ok", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return new RenderedLabelDto(result.Content, result.ContentType, null, result.Timings.ToServerTimingHeader(), result.Warnings) { Dpi = dpi };
    }
}
