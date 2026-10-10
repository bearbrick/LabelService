namespace LabelService.Domain.Shared;

/// <summary>
/// 工作单元：一次管理操作里通过仓储做的修改，调用 <see cref="SaveChangesAsync(string, CancellationToken)"/> 时一起写进主库。
/// </summary>
/// <remarks>
/// 每个请求一个实例，和这个请求里的仓储共用同一个数据库上下文。
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// 保存全部修改。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保存完成时完成。</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存全部修改；违反唯一约束或并发令牌（别人同时改了同一条）时抛 <see cref="UserFriendlyException"/>。
    /// </summary>
    /// <param name="conflictMessage">冲突时给人看的说明，如“草稿刚被别人发布了，请重新打开模板”。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保存完成时完成。</returns>
    /// <exception cref="UserFriendlyException">并发冲突或唯一约束冲突。</exception>
    Task SaveChangesAsync(string conflictMessage, CancellationToken cancellationToken = default);
}
