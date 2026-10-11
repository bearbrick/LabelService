using System.Diagnostics;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;

namespace LabelService.Api.Endpoints.OpenApi;

/// <summary>
/// 开放接口统一格式的失败响应：<see cref="ErrorResponse"/> + X-Trace-Id 头。
/// </summary>
internal static class ApiErrors
{
    /// <summary>
    /// 创建失败响应。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="code">错误码，见 <see cref="ErrorCodes"/>。</param>
    /// <param name="message">中文说明。</param>
    /// <param name="errors">明细，可空。</param>
    /// <param name="statusCode">HTTP 状态码；不传则按 <see cref="ErrorCodes.StatusCodes"/> 取，表里没有的按 500。</param>
    /// <returns>JSON 错误响应。</returns>
    public static IResult Create(HttpContext http, string code, string message, IReadOnlyList<ErrorDetail>? errors = null, int? statusCode = null)
    {
        var traceId = TraceId(http);
        http.Response.Headers[HeaderNames.TraceId] = traceId;
        var body = new ErrorResponse
        {
            Code = code,
            Message = message,
            Errors = errors?.ToList(),
            TraceId = traceId,
        };
        return Results.Json(body, LabelJson.Options, statusCode: statusCode ?? ErrorCodes.StatusCodes.GetValueOrDefault(code, 500));
    }

    /// <summary>
    /// 当前请求的链路 ID：有 Activity 时用 W3C traceparent，否则用 ASP.NET Core 的请求标识。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <returns>链路 ID。</returns>
    public static string TraceId(HttpContext http) => Activity.Current?.Id ?? http.TraceIdentifier;
}
