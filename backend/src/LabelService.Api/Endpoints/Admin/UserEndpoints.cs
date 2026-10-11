using LabelService.Application.Identity;
using LabelService.Application.Identity.Dtos;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 用户管理（权限点 sys.user）。规则见 <see cref="IUserAppService"/>。
/// </summary>
internal static class UserEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/users", (IUserAppService service, CancellationToken cancellationToken) => service.ListAsync(cancellationToken))
            .RequireAdmin(PermissionNames.UserManage);
        api.MapPost("/users", (CreateUserDto body, IUserAppService service) => service.CreateAsync(body))
            .RequireAdmin(PermissionNames.UserManage);
        api.MapPut("/users/{id:int}", (int id, UpdateUserDto body, IUserAppService service) => service.UpdateAsync(id, body))
            .RequireAdmin(PermissionNames.UserManage);
        api.MapPost("/users/{id:int}/status", (int id, SetStatusInput body, IUserAppService service) => service.SetStatusAsync(id, body))
            .RequireAdmin(PermissionNames.UserManage);
        api.MapPost("/users/{id:int}/reset-password", (int id, IUserAppService service) => service.ResetPasswordAsync(id))
            .RequireAdmin(PermissionNames.UserManage);
    }
}
