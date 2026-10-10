using LabelService.Api.Security;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 管理接口 /api/admin 的路由注册（只给设计器和管理后台用，不对外公开）。接口说明见 docs/admin-api.md。
/// </summary>
internal static class AdminEndpoints
{
    /// <summary>
    /// 注册全部管理接口。
    /// </summary>
    /// <param name="app">路由。</param>
    /// <returns>同一个路由，便于链式调用。</returns>
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/admin");
        AccountEndpoints.Map(api);
        UserEndpoints.Map(api);
        RoleEndpoints.Map(api);
        BusinessEndpoints.Map(api);
        TemplateEndpoints.Map(api);
        ResourceEndpoints.Map(api);
        ApiClientEndpoints.Map(api);
        LogEndpoints.Map(api);
        PreviewEndpoints.Map(api);
        LayoutEndpoints.Map(api);
        return app;
    }

    /// <summary>
    /// 要求登录；<paramref name="permission"/> 不为空时还要有这个权限点（必须先改密码的用户只能访问不带权限点的接口）。
    /// </summary>
    /// <param name="builder">路由。</param>
    /// <param name="permission">权限点，见 <see cref="Domain.Identity.PermissionNames"/>；null 表示登录即可。</param>
    /// <returns>同一个路由，便于链式调用。</returns>
    public static RouteHandlerBuilder RequireAdmin(this RouteHandlerBuilder builder, string? permission = null) =>
        builder.AddEndpointFilter(new AdminFilter(permission));

    /// <summary>
    /// 不要求登录（只有登录接口）：只把用户能修正的问题转成错误响应。
    /// </summary>
    /// <param name="builder">路由。</param>
    /// <returns>同一个路由，便于链式调用。</returns>
    public static RouteHandlerBuilder AllowAnonymousAdmin(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(new UserFriendlyErrorFilter());
}
