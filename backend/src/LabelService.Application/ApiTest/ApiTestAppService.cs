using LabelService.Application.Identity;
using LabelService.Application.Rendering;
using LabelService.Application.Rendering.Dtos;
using LabelService.Application.Shared;
using LabelService.Application.Templates;
using LabelService.Contracts.Protocol;
using LabelService.Domain.Shared;
using LabelService.Rendering;
using LabelService.Rendering.Validation;

namespace LabelService.Application.ApiTest;

/// <summary>
/// 接口调试的应用服务实现。
/// </summary>
/// <param name="cache">已发布模板的内存缓存。</param>
/// <param name="renderer">渲染引擎。</param>
/// <param name="currentAdmin">当前用户（业务范围）。</param>
/// <param name="clock">字段自动值用的当前时间。</param>
public sealed class ApiTestAppService(PublishedTemplateCache cache, ILabelRenderer renderer, ICurrentAdmin currentAdmin, AutoValueClock clock) : IApiTestAppService
{
    /// <inheritdoc />
    public RenderedLabelDto Render(RenderApiRequest request)
    {
        // 不在业务范围里的模板和不存在一样处理，不暴露是否存在。
        if (string.IsNullOrWhiteSpace(request.TemplateCode) || !cache.TryGetPublished(request.TemplateCode, request.Version, out var published)
            || !currentAdmin.Principal.InScope(published.BusinessId))
        {
            throw AdminErrors.NotFoundError($"模板 {request.TemplateCode} 不存在、版本不存在或还没有发布");
        }

        if (!LabelFormats.TryParse(request.Format, out var format) || !renderer.Supports(format))
        {
            throw new UserFriendlyException("format 只能是 pdf、png、zpl");
        }

        if (request.Dpi is { } dpi && !RenderLimits.AllowedDpi.Contains(dpi))
        {
            throw new UserFriendlyException("dpi 只能是 203、300、600");
        }

        // 份数夹在允许范围内；PNG 只出一张。
        var copies = Math.Clamp(request.Copies ?? 1, RenderLimits.MinCopies, RenderLimits.MaxCopies);
        if (format == LabelFormat.Png)
        {
            copies = 1;
        }

        // 接口调试的自动值：调用方写“接口调试”，请求号现生成一个
        var auto = clock.For(published.Template.Fields, 1, "接口调试", Guid.NewGuid().ToString("N"));
        var validation = FieldValidator.Validate(published.Template.Fields, null, request.Data ?? [], null, auto);
        if (!validation.IsValid)
        {
            throw new UserFriendlyException(
                validation.Errors[0].Message ?? validation.Errors[0].Code,
                validation.Errors.Select(error => $"{error.Code}：{error.Message}").ToArray());
        }

        try
        {
            var result = renderer.Render(new RenderJob
            {
                Template = published.Template,
                Labels = [new LabelInstance(validation.Values, copies)],
                Format = format,
                Dpi = request.Dpi ?? (published.Template.Page.Dpi > 0 ? published.Template.Page.Dpi : RenderLimits.DefaultDpi),
                Mono = request.Mono ?? false,
            });
            var fileName = $"{published.Template.Code}_test.{LabelFormats.Extension(format)}";
            return new RenderedLabelDto(result.Content, result.ContentType, fileName, result.Timings.ToServerTimingHeader(), result.Warnings)
            {
                TemplateVersion = published.Version,
                LabelCount = result.LabelCount,
            };
        }
        catch (LabelRenderException ex)
        {
            throw new UserFriendlyException($"{ex.ErrorCode}：{ex.Message}");
        }
    }
}
