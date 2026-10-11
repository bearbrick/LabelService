using LabelService.Application.Identity;

namespace LabelService.Api.Security;

/// <summary>
/// <see cref="ICurrentAdmin"/> 的实现：当前用户由 <see cref="AdminFilter"/> 登记，来源 IP 从 HTTP 连接取。每个请求一个。
/// </summary>
/// <param name="accessor">当前 HTTP 请求。</param>
internal sealed class HttpCurrentAdmin(IHttpContextAccessor accessor) : ICurrentAdmin
{
    /// <summary>
    /// 登记的当前用户；没经过 <see cref="AdminFilter"/> 时为 null。
    /// </summary>
    private AdminPrincipal? principal;

    /// <inheritdoc />
    public AdminPrincipal Principal => principal ?? throw new InvalidOperationException("管理接口没有经过 AdminFilter");

    /// <inheritdoc />
    public string ClientIp => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "";

    /// <summary>
    /// 登记当前用户（<see cref="AdminFilter"/> 读完用户和角色后调用）。
    /// </summary>
    /// <param name="admin">当前用户。</param>
    public void SignIn(AdminPrincipal admin) => principal = admin;
}
