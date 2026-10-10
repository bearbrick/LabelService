using LabelService.Application.ApiClients;
using LabelService.Application.ApiClients.Dtos;
using LabelService.Application.Identity.Dtos;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 调用方与密钥（权限点 api.manage）：新建（返回一次 Key）、修改业务范围和 IP 白名单、启停、重置 Key。
/// 业务范围的规则见 <see cref="IApiClientAppService"/>。
/// </summary>
internal static class ApiClientEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/clients", (IApiClientAppService service, CancellationToken cancellationToken) => service.ListAsync(cancellationToken))
            .RequireAdmin(PermissionNames.ApiManage);
        api.MapPost("/clients", (SaveApiClientDto body, IApiClientAppService service) => service.CreateAsync(body))
            .RequireAdmin(PermissionNames.ApiManage);
        api.MapPut("/clients/{id:int}", (int id, SaveApiClientDto body, IApiClientAppService service) => service.UpdateAsync(id, body))
            .RequireAdmin(PermissionNames.ApiManage);
        api.MapPost("/clients/{id:int}/status", (int id, SetStatusInput body, IApiClientAppService service) => service.SetStatusAsync(id, body.Enabled))
            .RequireAdmin(PermissionNames.ApiManage);
        api.MapPost("/clients/{id:int}/reset-key", (int id, IApiClientAppService service) => service.ResetKeyAsync(id))
            .RequireAdmin(PermissionNames.ApiManage);
    }
}
