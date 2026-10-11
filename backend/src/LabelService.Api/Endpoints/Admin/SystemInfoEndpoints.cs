using LabelService.Application.Resources.Dtos;
using LabelService.Application.SystemSettings;
using LabelService.Application.SystemSettings.Dtos;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 系统信息和公司 Logo。读取不用登录（登录页要显示系统名称、Logo 和版权信息），修改要权限点 sys.settings。
/// </summary>
internal static class SystemInfoEndpoints
{
    /// <summary>
    /// Logo 文件的内容安全策略：SVG 上传时清洗过，再加一层，防止直接打开时执行任何东西。
    /// </summary>
    private const string LogoContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";

    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/system-info", (ISystemInfoAppService service, CancellationToken cancellationToken) => service.GetAsync(cancellationToken))
            .AllowAnonymousAdmin();
        api.MapPut("/system-info", (SaveSystemInfoDto? body, ISystemInfoAppService service) => service.SaveAsync(body))
            .RequireAdmin(PermissionNames.SettingsManage);
        api.MapGet("/system-info/logo", GetLogoAsync)
            .AllowAnonymousAdmin();
        api.MapPost("/system-info/logo", (IFormFile? file, ISystemInfoAppService service) =>
                service.UploadLogoAsync(file is null ? null : new UploadAssetInput(file.FileName, file.Length, file.OpenReadStream())))
            .RequireAdmin(PermissionNames.SettingsManage)
            .DisableAntiforgery();
        api.MapDelete("/system-info/logo", (ISystemInfoAppService service) => service.ResetLogoAsync())
            .RequireAdmin(PermissionNames.SettingsManage);
    }

    /// <summary>
    /// 公司 Logo 文件：带修改时间（浏览器按修改时间重新验证）、内容安全策略和禁止猜测类型的响应头。
    /// 地址后面的 ?v= 由前端按 Logo 版本拼上，换图后地址变化，不会显示缓存里的旧图。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="service">系统信息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>图片文件。</returns>
    private static async Task<IResult> GetLogoAsync(HttpContext http, ISystemInfoAppService service, CancellationToken cancellationToken)
    {
        var file = await service.GetLogoAsync(cancellationToken);
        http.Response.Headers.ContentSecurityPolicy = LogoContentSecurityPolicy;
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.CacheControl = "no-cache";
        return Results.File(file.Content, file.ContentType, enableRangeProcessing: false, lastModified: file.LastModified);
    }
}
