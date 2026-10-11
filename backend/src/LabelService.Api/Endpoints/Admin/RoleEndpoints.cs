using LabelService.Application.Identity;
using LabelService.Application.Identity.Dtos;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 角色权限：查看登录即可，新建、修改、删除要权限点 sys.role。规则见 <see cref="IRoleAppService"/>。
/// </summary>
internal static class RoleEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/roles", (IRoleAppService service, CancellationToken cancellationToken) => service.ListAsync(cancellationToken))
            .RequireAdmin();
        api.MapPost("/roles", (CreateRoleDto body, IRoleAppService service) => service.CreateAsync(body))
            .RequireAdmin(PermissionNames.RoleManage);
        api.MapPut("/roles/{id:int}", (int id, UpdateRoleDto body, IRoleAppService service) => service.UpdateAsync(id, body))
            .RequireAdmin(PermissionNames.RoleManage);
        api.MapDelete("/roles/{id:int}", async (int id, IRoleAppService service) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.RoleManage);
    }
}
