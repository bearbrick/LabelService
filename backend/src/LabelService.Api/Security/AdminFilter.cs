using LabelService.Application.Identity;
using LabelService.Application.Shared;
using LabelService.Domain.Identity;
using LabelService.Domain.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace LabelService.Api.Security;

/// <summary>
/// 管理接口的过滤器：要求登录，从库里读用户（停用、改角色立即生效），检查权限点；必须先改密码时只放行个人设置。
/// 也负责把应用服务抛出的 <see cref="UserFriendlyException"/> 转成错误响应。
/// </summary>
/// <param name="permission">要求的权限点；null 表示登录即可。</param>
internal sealed class AdminFilter(string? permission) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var result = await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!result.Succeeded || !int.TryParse(result.Principal?.Identity?.Name, out var userId))
        {
            return AdminResults.Error(AdminErrors.Unauthorized, "请先登录");
        }

        // 每次请求都从库里读用户和角色：停用、改角色、改权限立即生效。
        var services = http.RequestServices;
        var user = await services.GetRequiredService<IUserRepository>().FindAsync(userId, http.RequestAborted);
        var role = user is null ? null : await services.GetRequiredService<IRoleRepository>().FindAsync(user.RoleId, http.RequestAborted);
        if (user is not { Status: UserStatuses.Active } || role is null)
        {
            return AdminResults.Error(AdminErrors.Unauthorized, "账号已停用或不存在，请重新登录");
        }

        var principal = new AdminPrincipal(user, role);
        services.GetRequiredService<HttpCurrentAdmin>().SignIn(principal);
        if (user.MustChangePassword && permission is not null)
        {
            return AdminResults.Error(AdminErrors.PasswordChangeRequired, "请先在个人设置里修改密码");
        }

        if (permission is not null && !principal.Can(permission))
        {
            return AdminResults.Error(AdminErrors.Forbidden, "没有权限做这个操作");
        }

        return await InvokeHandlerAsync(context, next);
    }

    /// <summary>
    /// 调用处理函数，把用户能修正的问题转成错误响应。
    /// </summary>
    /// <param name="context">过滤器上下文。</param>
    /// <param name="next">处理函数。</param>
    /// <returns>处理函数的结果，或错误响应。</returns>
    internal static async ValueTask<object?> InvokeHandlerAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (UserFriendlyException ex)
        {
            return AdminResults.From(ex);
        }
    }
}

/// <summary>
/// 不要求登录的管理接口（登录）用的过滤器：只把 <see cref="UserFriendlyException"/> 转成错误响应。
/// </summary>
internal sealed class UserFriendlyErrorFilter : IEndpointFilter
{
    /// <inheritdoc />
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        AdminFilter.InvokeHandlerAsync(context, next);
}
