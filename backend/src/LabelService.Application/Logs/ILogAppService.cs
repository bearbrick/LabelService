using LabelService.Application.Logs.Dtos;

namespace LabelService.Application.Logs;

/// <summary>
/// 调用日志、操作日志（权限点 log.view）。调用日志按当前用户的业务范围只看自己范围里的模板。
/// </summary>
/// <remarks>
/// 两种日志都按时间倒序翻页，翻页用“上一页最后一条”（keyset），越往后翻越不变慢。
/// </remarks>
public interface ILogAppService
{
    /// <summary>
    /// 查询一页调用日志。
    /// </summary>
    /// <param name="input">查询条件。</param>
    /// <returns>日志，新的在前。</returns>
    Task<IReadOnlyList<RenderLogDto>> SearchRenderLogsAsync(RenderLogSearchInput input);

    /// <summary>
    /// 查询一页操作日志。
    /// </summary>
    /// <param name="input">查询条件。</param>
    /// <returns>日志，新的在前。</returns>
    Task<IReadOnlyList<OperationLogDto>> SearchOperationLogsAsync(OperationLogSearchInput input);
}
