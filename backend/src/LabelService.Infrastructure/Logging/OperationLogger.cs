using System.Data.Common;
using LabelService.Application.Identity;
using LabelService.Application.Logs;
using LabelService.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LabelService.Infrastructure.Logging;

/// <summary>
/// 后台操作日志的实现：直接写进日志库的 op_log 表。管理操作不在热路径上，同步写；写失败只记警告，不影响操作本身。
/// </summary>
/// <param name="repository">操作日志仓储。</param>
/// <param name="currentAdmin">当前用户和来源 IP。</param>
/// <param name="clock">时间来源。</param>
/// <param name="logger">日志。</param>
internal sealed class OperationLogger(
    IOperationLogRepository repository,
    ICurrentAdmin currentAdmin,
    TimeProvider clock,
    ILogger<OperationLogger> logger) : IOperationLogger
{
    /// <inheritdoc />
    public Task WriteAsync(string type, string target, string detail)
    {
        var principal = currentAdmin.Principal;
        return WriteAsync(principal.User.Id, principal.User.Name, type, target, detail);
    }

    /// <inheritdoc />
    public async Task WriteAsync(int? userId, string userName, string type, string target, string detail)
    {
        try
        {
            // 超长的内容截断到列的长度，不让一条日志写失败。
            await repository.AddAsync(new OperationLogEntity
            {
                UserId = userId,
                UserName = Truncate(userName, 64),
                Type = Truncate(type, 32),
                Target = Truncate(target, 256),
                Detail = Truncate(detail, 1024),
                Ip = currentAdmin.ClientIp,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            });
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or DbException)
        {
            logger.LogWarning(ex, "操作日志写入失败：{Type} {Target}", type, target);
        }
    }

    /// <summary>
    /// 截断到指定长度。
    /// </summary>
    /// <param name="value">原文。</param>
    /// <param name="length">最大长度。</param>
    /// <returns>不超过 <paramref name="length"/> 的字符串。</returns>
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
