namespace LabelService.Application.Identity;

/// <summary>
/// 当前请求的后台用户和来源 IP。接口层实现（从 HTTP 请求里取），应用服务只依赖这个接口。
/// </summary>
public interface ICurrentAdmin
{
    /// <summary>
    /// 当前登录的后台用户。
    /// </summary>
    /// <exception cref="InvalidOperationException">请求没有经过登录检查（程序缺陷）。</exception>
    AdminPrincipal Principal
    {
        get;
    }

    /// <summary>
    /// 请求的来源 IP；未知时为空字符串。
    /// </summary>
    string ClientIp
    {
        get;
    }
}
