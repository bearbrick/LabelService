using LabelService.Application.Rendering.Dtos;
using LabelService.Application.Templates.Dtos;

namespace LabelService.Application.Templates;

/// <summary>
/// 服务端预览（权限点 tpl.view）：模板不经过存储，直接检查后用服务端渲染引擎出 PNG，保证所见即所得。
/// </summary>
/// <remarks>
/// 放在管理接口而不是开放接口，因为它要登录，而登录态和 API Key 互不相通（ADR 0006、ADR 0007）。
/// 指标只记 endpoint=preview 的请求数和总耗时，不影响开放接口的指标；不写调用日志。
/// </remarks>
public interface ITemplatePreviewAppService
{
    /// <summary>
    /// 渲染预览图。
    /// </summary>
    /// <param name="input">模板、数据、分辨率、是否黑白。</param>
    /// <returns>PNG 和响应头要用的分辨率、各阶段耗时、提示。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">缺少模板、模板有问题、DPI 不支持、数据不合法、渲染失败。</exception>
    RenderedLabelDto Render(PreviewInput input);
}
