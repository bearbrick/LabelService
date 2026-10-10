using LabelService.Api.Security;
using LabelService.Application.Identity;
using LabelService.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LabelService.Backend.Checks;

/// <summary>
/// 检查程序直接调用应用服务时用：创建一个作用域（相当于一次请求），并以某个后台用户的身份登录。
/// </summary>
internal static class AdminScope
{
    /// <summary>
    /// 创建作用域并登录。用完要释放（<c>await using</c>）。
    /// </summary>
    /// <param name="services">服务的根容器（<c>factory.Services</c>）。</param>
    /// <param name="account">登录的账号，默认首次启动建的 admin。</param>
    /// <returns>已登录的作用域。</returns>
    /// <exception cref="InvalidOperationException">账号或它的角色不存在。</exception>
    public static async Task<AsyncServiceScope> CreateAsync(IServiceProvider services, string account = "admin")
    {
        var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var user = await provider.GetRequiredService<IUserRepository>().FindByAccountAsync(account)
            ?? throw new InvalidOperationException($"账号 {account} 不存在");
        var role = await provider.GetRequiredService<IRoleRepository>().FindAsync(user.RoleId)
            ?? throw new InvalidOperationException($"账号 {account} 的角色不存在");
        provider.GetRequiredService<HttpCurrentAdmin>().SignIn(new AdminPrincipal(user, role));
        return scope;
    }

    /// <summary>
    /// 以 admin 的身份调用一次应用服务（每次一个新作用域）。
    /// </summary>
    /// <typeparam name="TService">应用服务接口。</typeparam>
    /// <typeparam name="TResult">返回值类型。</typeparam>
    /// <param name="services">服务的根容器。</param>
    /// <param name="action">要做的调用。</param>
    /// <returns>调用的返回值。</returns>
    public static async Task<TResult> RunAsync<TService, TResult>(IServiceProvider services, Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        await using var scope = await CreateAsync(services);
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
