using LabelService.Application.Identity;
using LabelService.Application.Identity.Dtos;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 账号：登录、退出、当前用户、个人设置（改姓名手机号、改密码）、权限目录。
/// </summary>
internal static class AccountEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/login", (LoginInput body, IAccountAppService service) => service.LoginAsync(body)).AllowAnonymousAdmin();
        api.MapPost("/logout", async (IAccountAppService service) =>
        {
            await service.LogoutAsync();
            return Results.NoContent();
        }).RequireAdmin();
        api.MapGet("/me", (IAccountAppService service) => service.GetCurrentUser()).RequireAdmin();
        api.MapPut("/me", async (UpdateProfileInput body, IAccountAppService service) =>
        {
            await service.UpdateProfileAsync(body);
            return Results.NoContent();
        }).RequireAdmin();
        api.MapPost("/me/password", async (ChangePasswordInput body, IAccountAppService service) =>
        {
            await service.ChangePasswordAsync(body);
            return Results.NoContent();
        }).RequireAdmin();
        api.MapGet("/permissions", (IAccountAppService service) => service.ListPermissions()).RequireAdmin();
    }
}
