using LabelService.Application.Rendering.Dtos;
using LabelService.Contracts.Protocol;

namespace LabelService.Application.ApiTest;

/// <summary>
/// 接口调试（权限点 api.test）：在后台用已发布的模板试一次生成，请求体和开放接口一样，不需要 API Key。
/// </summary>
/// <remarks>
/// 管理接口和开放接口的鉴权互不相通（ADR 0006）：这里只认登录令牌，按用户的业务范围限制模板，不写调用日志。
/// </remarks>
public interface IApiTestAppService
{
    /// <summary>
    /// 生成一次标签。
    /// </summary>
    /// <param name="request">和开放接口 /api/v1/render 一样的请求体。</param>
    /// <returns>文件和响应头要用的模板版本、张数、各阶段耗时、提示。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">模板不存在、没发布或不在范围里（NOT_FOUND），格式、DPI、数据不合法，渲染失败。</exception>
    RenderedLabelDto Render(RenderApiRequest request);
}
