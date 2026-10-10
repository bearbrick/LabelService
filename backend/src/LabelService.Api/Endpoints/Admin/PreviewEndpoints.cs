using System.Text.Json;
using LabelService.Api.Security;
using LabelService.Application.ApiTest;
using LabelService.Application.Shared;
using LabelService.Application.Templates;
using LabelService.Application.Templates.Dtos;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 服务端预览（权限点 tpl.view）和接口调试（权限点 api.test）。
/// </summary>
/// <remarks>
/// 预览：设计器把未保存的模板 JSON 和样例数据发过来，用服务端渲染引擎出 PNG，保证所见即所得（ADR 0007）。
/// 请求体自己解析，JSON 不合法时告诉设计器出错的位置。
/// </remarks>
internal static class PreviewEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/preview", PreviewAsync).RequireAdmin(PermissionNames.TemplateView);
        api.MapPost("/api-test/render", (RenderApiRequest body, HttpContext http, IApiTestAppService service) => RenderedLabelResults.ToResult(http, service.Render(body)))
            .RequireAdmin(PermissionNames.ApiTest);
    }

    /// <summary>
    /// JSON 解析错误的位置：JSON 路径和行列（行列从 1 开始），方便在设计器里定位。
    /// </summary>
    /// <param name="ex">解析错误。</param>
    /// <returns>给人看的说明。</returns>
    internal static string JsonErrorMessage(JsonException ex)
    {
        var path = string.IsNullOrEmpty(ex.Path) ? "$" : ex.Path;
        var position = ex.LineNumber is { } line ? $"，第 {line + 1} 行第 {(ex.BytePositionInLine ?? 0) + 1} 个字节" : "";
        return $"模板 JSON 不合法，位置 {path}{position}";
    }

    /// <summary>
    /// 服务端预览：解析请求体，渲染 PNG。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="service">服务端预览。</param>
    /// <returns>PNG，或错误响应。</returns>
    private static async Task<IResult> PreviewAsync(HttpContext http, ITemplatePreviewAppService service)
    {
        PreviewInput? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PreviewInput>(http.Request.Body, LabelJson.Options, http.RequestAborted);
        }
        catch (JsonException ex)
        {
            return AdminResults.Error(AdminErrors.Invalid, JsonErrorMessage(ex), [ex.Message]);
        }

        return RenderedLabelResults.ToResult(http, service.Render(body ?? new PreviewInput(null, null, null, null)));
    }
}
