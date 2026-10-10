using LabelService.Application.Businesses;
using LabelService.Application.Businesses.Dtos;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 业务分组：列表按用户的业务范围过滤（登录即可看）；新建、修改、删除要权限点 biz.manage。
/// </summary>
internal static class BusinessEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/businesses", (IBusinessAppService service, CancellationToken cancellationToken) => service.ListAsync(cancellationToken))
            .RequireAdmin();
        api.MapPost("/businesses", (SaveBusinessDto body, IBusinessAppService service) => service.CreateAsync(body))
            .RequireAdmin(PermissionNames.BusinessManage);
        api.MapPut("/businesses/{id:int}", (int id, SaveBusinessDto body, IBusinessAppService service) => service.UpdateAsync(id, body))
            .RequireAdmin(PermissionNames.BusinessManage);
        api.MapDelete("/businesses/{id:int}", async (int id, IBusinessAppService service) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.BusinessManage);
    }
}
