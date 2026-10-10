using LabelService.Application.Shared;
using LabelService.Domain.Shared;

namespace LabelService.Api.Security;

/// <summary>
/// 管理接口的错误响应。格式统一是 <c>{ code, message, details }</c>，前端按 code 区分（401 UNAUTHORIZED 跳登录页等）。
/// </summary>
internal static class AdminResults
{
    /// <summary>
    /// 错误码 → HTTP 状态码。不在表里的按 400。
    /// </summary>
    private static readonly Dictionary<string, int> StatusCodesByCode = new(StringComparer.Ordinal)
    {
        [AdminErrors.Invalid] = StatusCodes.Status400BadRequest,
        [AdminErrors.NotFound] = StatusCodes.Status404NotFound,
        [AdminErrors.Forbidden] = StatusCodes.Status403Forbidden,
        [AdminErrors.Unauthorized] = StatusCodes.Status401Unauthorized,
        [AdminErrors.PasswordChangeRequired] = StatusCodes.Status403Forbidden,
        [AdminErrors.InvalidLogin] = StatusCodes.Status401Unauthorized,
        [AdminErrors.Locked] = StatusCodes.Status403Forbidden,
        [AdminErrors.Disabled] = StatusCodes.Status403Forbidden,
    };

    /// <summary>
    /// 错误响应。
    /// </summary>
    /// <param name="code">错误码，见 <see cref="AdminErrors"/>。</param>
    /// <param name="message">给人看的说明。</param>
    /// <param name="details">逐条问题；没有时为空数组。</param>
    /// <returns>JSON 错误响应，状态码按错误码决定。</returns>
    public static IResult Error(string code, string message, IReadOnlyList<string>? details = null) =>
        Results.Json(new AdminErrorResponse(code, message, details ?? []), statusCode: StatusCodesByCode.GetValueOrDefault(code, StatusCodes.Status400BadRequest));

    /// <summary>
    /// 把应用服务抛出的 <see cref="UserFriendlyException"/> 转成错误响应。
    /// </summary>
    /// <param name="ex">异常。</param>
    /// <returns>JSON 错误响应。</returns>
    public static IResult From(UserFriendlyException ex) => Error(ex.Code, ex.Message, ex.Details);
}

/// <summary>
/// 管理接口的错误响应体。
/// </summary>
/// <param name="Code">错误码：INVALID、NOT_FOUND、FORBIDDEN、UNAUTHORIZED、PASSWORD_CHANGE_REQUIRED、INVALID_LOGIN、LOCKED、DISABLED。</param>
/// <param name="Message">给人看的说明。</param>
/// <param name="Details">逐条问题，如发布检查不通过的原因。</param>
internal sealed record AdminErrorResponse(string Code, string Message, IReadOnlyList<string> Details);
