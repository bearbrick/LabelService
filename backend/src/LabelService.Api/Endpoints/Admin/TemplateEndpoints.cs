using LabelService.Application.Templates;
using LabelService.Application.Templates.Dtos;
using LabelService.Contracts.Templates;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 模板管理：列表、打开、缩略图、新建、保存草稿、放弃草稿、发布、版本、回滚、换分组、删除、发布前检查。
/// 每个操作要对应的权限点；业务范围由 <see cref="ITemplateAppService"/> 检查。
/// </summary>
internal static class TemplateEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        // 列表、新建、发布前检查、打开、缩略图、删除。
        api.MapGet("/templates", (ITemplateAppService service, CancellationToken cancellationToken) => service.ListAsync(cancellationToken))
            .RequireAdmin(PermissionNames.TemplateView);
        api.MapPost("/templates", (CreateTemplateDto body, ITemplateAppService service) => service.CreateAsync(body))
            .RequireAdmin(PermissionNames.TemplateEdit);
        api.MapPost("/templates/check", (LabelTemplate body, ITemplateAppService service) => service.Check(body))
            .RequireAdmin(PermissionNames.TemplateView);
        api.MapGet("/templates/{code}", (string code, ITemplateAppService service) => service.GetAsync(code))
            .RequireAdmin(PermissionNames.TemplateView);
        api.MapGet("/templates/{code}/thumbnail", ThumbnailAsync)
            .RequireAdmin(PermissionNames.TemplateView);
        api.MapDelete("/templates/{code}", async (string code, ITemplateAppService service) =>
        {
            await service.DeleteAsync(code);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.TemplateDelete);
        // 草稿：保存、放弃。
        api.MapPut("/templates/{code}/draft", (string code, LabelTemplate body, ITemplateAppService service) => service.SaveDraftAsync(code, body))
            .RequireAdmin(PermissionNames.TemplateEdit);
        api.MapDelete("/templates/{code}/draft", async (string code, ITemplateAppService service) =>
        {
            await service.DiscardDraftAsync(code);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.TemplateEdit);
        // 发布、版本、回滚、换分组。
        api.MapPost("/templates/{code}/publish", (string code, PublishTemplateDto? body, ITemplateAppService service) => service.PublishAsync(code, body))
            .RequireAdmin(PermissionNames.TemplatePublish);
        api.MapGet("/templates/{code}/versions", (string code, ITemplateAppService service) => service.ListVersionsAsync(code))
            .RequireAdmin(PermissionNames.TemplateView);
        api.MapGet("/templates/{code}/versions/{version:int}", (string code, int version, ITemplateAppService service) => service.GetVersionAsync(code, version))
            .RequireAdmin(PermissionNames.TemplateView);
        api.MapPost("/templates/{code}/versions/{version:int}/restore", (string code, int version, ITemplateAppService service) => service.RestoreAsync(code, version))
            .RequireAdmin(PermissionNames.TemplateEdit);
        api.MapPut("/templates/{code}/business", async (string code, MoveTemplateDto body, ITemplateAppService service) =>
        {
            await service.MoveAsync(code, body);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.TemplateEdit);
    }

    /// <summary>
    /// 缩略图（PNG，浏览器缓存 60 秒）；没有内容或渲染失败时 204，前端显示占位图。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="http">当前请求。</param>
    /// <param name="service">模板管理。</param>
    /// <returns>PNG 或 204。</returns>
    private static async Task<IResult> ThumbnailAsync(string code, HttpContext http, ITemplateAppService service)
    {
        var png = await service.GetThumbnailAsync(code);
        if (png is null)
        {
            return Results.NoContent();
        }

        http.Response.Headers.CacheControl = "private, max-age=60";
        return Results.File(png, "image/png");
    }
}
