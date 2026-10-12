namespace LabelService.Application.Logs;

/// <summary>
/// 后台操作日志：登录、退出和所有写操作各记一条（ADR 0006），写在日志库的 op_log 表。
/// </summary>
/// <remarks>
/// 写失败只记警告，不影响操作本身。
/// </remarks>
public interface IOperationLogger
{
    /// <summary>
    /// 记当前登录用户的一条操作日志。
    /// </summary>
    /// <param name="type">操作类型，如“发布模板”。</param>
    /// <param name="target">操作对象，如“BOX_LABEL 成品箱标”。</param>
    /// <param name="detail">说明。</param>
    /// <returns>写完时完成。</returns>
    Task WriteAsync(string type, string target, string detail);

    /// <summary>
    /// 记一条指定操作人的操作日志（登录时还没有当前用户）。
    /// </summary>
    /// <param name="userId">操作人 ID；登录失败、账号不存在时为 null。</param>
    /// <param name="userName">操作人姓名，或尝试登录的账号。</param>
    /// <param name="type">操作类型。</param>
    /// <param name="target">操作对象。</param>
    /// <param name="detail">说明。</param>
    /// <returns>写完时完成。</returns>
    Task WriteAsync(int? userId, string userName, string type, string target, string detail);
}
