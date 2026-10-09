using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Admin;

/// <summary>
/// 管理接口的响应。错误统一是 <c>{ code, message, details }</c>，前端按 code 区分（401 UNAUTHORIZED 跳登录页等）。
/// </summary>
internal static class AdminResults
{
    /// <summary>
    /// 错误响应。
    /// </summary>
    public static IResult Error(int status, string code, string message, IReadOnlyList<string>? details = null) =>
        Results.Json(new AdminError(code, message, details ?? []), statusCode: status);

    /// <summary>
    /// 操作者能修正的问题：400 INVALID。
    /// </summary>
    public static IResult Invalid(string message, IReadOnlyList<string>? details = null) =>
        Error(StatusCodes.Status400BadRequest, "INVALID", message, details);

    /// <summary>
    /// 找不到，或者不在自己的业务范围里（不暴露是否存在）：404 NOT_FOUND。
    /// </summary>
    public static IResult NotFound(string message) => Error(StatusCodes.Status404NotFound, "NOT_FOUND", message);

    /// <summary>
    /// 没有权限：403 FORBIDDEN。
    /// </summary>
    public static IResult Forbidden(string message) => Error(StatusCodes.Status403Forbidden, "FORBIDDEN", message);

    /// <summary>
    /// 把 <see cref="ManagementException"/> 转成 400。
    /// </summary>
    public static IResult From(ManagementException ex) => Invalid(ex.Message, ex.Details);

    /// <summary>
    /// 当前登录用户（<see cref="AdminFilter"/> 放进来的）。
    /// </summary>
    public static AdminUser CurrentAdmin(this HttpContext http) =>
        http.Items[AdminFilter.ItemKey] as AdminUser ?? throw new InvalidOperationException("管理接口没有经过 AdminFilter");

    /// <summary>
    /// 来源 IP。
    /// </summary>
    public static string ClientIp(this HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "";
}

/// <summary>
/// 管理接口的错误。
/// </summary>
/// <param name="Code">错误码：UNAUTHORIZED、FORBIDDEN、PASSWORD_CHANGE_REQUIRED、INVALID、NOT_FOUND、LOCKED。</param>
/// <param name="Message">给人看的说明。</param>
/// <param name="Details">逐条问题，如发布检查不通过的原因。</param>
internal sealed record AdminError(string Code, string Message, IReadOnlyList<string> Details);

/// <summary>
/// 后台操作日志：登录、退出和所有写操作各记一条（ADR 0006），写在日志库的 op_log 表。
/// </summary>
/// <remarks>
/// 管理操作不在热路径上，直接同步写；写失败只记警告，不影响操作本身。
/// </remarks>
internal sealed class OperationLog(IDbContextFactory<LogDbContext> contexts, TimeProvider clock, ILogger<OperationLog> logger)
{
    /// <summary>
    /// 记一条操作日志。
    /// </summary>
    public async Task WriteAsync(HttpContext http, int? userId, string userName, string type, string target, string detail)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync();
            db.OpLogs.Add(new OpLogRecord
            {
                UserId = userId,
                UserName = Truncate(userName, 64),
                Type = Truncate(type, 32),
                Target = Truncate(target, 256),
                Detail = Truncate(detail, 1024),
                Ip = http.ClientIp(),
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            logger.LogWarning(ex, "操作日志写入失败：{Type} {Target}", type, target);
        }
    }

    /// <summary>
    /// 记当前登录用户的一条操作日志。
    /// </summary>
    public Task WriteAsync(HttpContext http, string type, string target, string detail)
    {
        var admin = http.CurrentAdmin();
        return WriteAsync(http, admin.User.Id, admin.User.Name, type, target, detail);
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
