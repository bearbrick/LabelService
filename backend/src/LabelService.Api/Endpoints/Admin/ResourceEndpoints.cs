using LabelService.Application.Resources;
using LabelService.Application.Resources.Dtos;
using LabelService.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 画布预设、素材、字体。查看登录即可（设计器要用），增删改和上传字体要权限点 res.manage。
/// </summary>
internal static class ResourceEndpoints
{
    /// <summary>
    /// 素材文件的内容安全策略：SVG 都清洗过，再加一层，防止直接打开时执行任何东西。
    /// </summary>
    private const string AssetContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";

    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/presets", (ICanvasPresetAppService service, CancellationToken cancellationToken) => service.ListAsync(cancellationToken))
            .RequireAdmin();
        api.MapPost("/presets", (SaveCanvasPresetDto body, ICanvasPresetAppService service) => service.CreateAsync(body))
            .RequireAdmin(PermissionNames.ResourceManage);
        api.MapPut("/presets/{id:int}", (int id, SaveCanvasPresetDto body, ICanvasPresetAppService service) => service.UpdateAsync(id, body))
            .RequireAdmin(PermissionNames.ResourceManage);
        api.MapDelete("/presets/{id:int}", async (int id, ICanvasPresetAppService service) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.ResourceManage);

        api.MapGet("/assets", (IAssetAppService service, CancellationToken cancellationToken) => service.ListAsync(cancellationToken))
            .RequireAdmin();
        api.MapGet("/assets/{key}/file", GetAssetFile)
            .RequireAdmin();
        api.MapPost("/assets", (IFormFile? file, IAssetAppService service) =>
                service.UploadAsync(file is null ? null : new UploadAssetInput(file.FileName, file.Length, file.OpenReadStream())))
            .RequireAdmin(PermissionNames.ResourceManage)
            .DisableAntiforgery();
        api.MapPut("/assets/{key}", async (string key, RenameAssetDto body, IAssetAppService service) =>
        {
            await service.RenameAsync(key, body);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.ResourceManage);
        api.MapDelete("/assets/{key}", async (string key, IAssetAppService service) =>
        {
            await service.DeleteAsync(key);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.ResourceManage);

        api.MapGet("/fonts", (IFontAppService service) => service.List())
            .RequireAdmin();

        // 字体文件常有十几到几十 MB，比服务整体的请求体上限（20 MB）大，这个接口单独放宽到 51 MB（字体 50 MB 加表单开销）。
        api.MapPost("/fonts", (IFormFile? file, IFontAppService service) =>
                service.UploadAsync(file is null ? null : new UploadAssetInput(file.FileName, file.Length, file.OpenReadStream())))
            .RequireAdmin(PermissionNames.ResourceManage)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(FontAppService.MaxFontBytes + (1024 * 1024)));
        api.MapDelete("/fonts/{file}", (string file, IFontAppService service) => service.DeleteAsync(file))
            .RequireAdmin(PermissionNames.ResourceManage);
    }

    /// <summary>
    /// 素材文件，设计器显示用：带修改时间（浏览器缓存）、内容安全策略和禁止猜测类型的响应头。
    /// </summary>
    /// <param name="key">素材 Key。</param>
    /// <param name="http">当前请求。</param>
    /// <param name="service">素材。</param>
    /// <returns>图片文件。</returns>
    private static IResult GetAssetFile(string key, HttpContext http, IAssetAppService service)
    {
        var file = service.GetFile(key);
        http.Response.Headers.ContentSecurityPolicy = AssetContentSecurityPolicy;
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(file.Content, file.ContentType, enableRangeProcessing: false, lastModified: file.LastModified);
    }
}
