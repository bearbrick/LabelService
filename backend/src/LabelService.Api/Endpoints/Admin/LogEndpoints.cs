using LabelService.Application.Dashboard;
using LabelService.Application.Logs;
using LabelService.Application.Logs.Dtos;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 调用日志、操作日志（权限点 log.view）和概览页（权限点 tpl.view）。
/// </summary>
internal static class LogEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/call-logs", SearchRenderLogsAsync).RequireAdmin(PermissionNames.LogView);
        api.MapGet("/op-logs", SearchOperationLogsAsync).RequireAdmin(PermissionNames.LogView);
        api.MapGet("/dashboard", (IDashboardAppService service) => service.GetAsync()).RequireAdmin(PermissionNames.TemplateView);
    }

    /// <summary>
    /// 查询一页调用日志。
    /// </summary>
    /// <param name="service">日志查询。</param>
    /// <param name="from">起始时间（含）。</param>
    /// <param name="to">结束时间（不含）。</param>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="clientId">调用方 ID。</param>
    /// <param name="status">结果：ok、error、aborted。</param>
    /// <param name="q">按链路 ID、X-Request-Id、模板编码或错误码精确查找。</param>
    /// <param name="beforeTime">翻页：上一页最后一条的时间。</param>
    /// <param name="beforeId">翻页：上一页最后一条的 ID。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <returns>日志，新的在前。</returns>
    private static Task<IReadOnlyList<RenderLogDto>> SearchRenderLogsAsync(
        ILogAppService service,
        DateTime? from,
        DateTime? to,
        string? templateCode,
        int? clientId,
        string? status,
        string? q,
        DateTime? beforeTime,
        long? beforeId,
        int? pageSize) =>
        service.SearchRenderLogsAsync(new RenderLogSearchInput(from, to, templateCode, clientId, status, q, beforeTime, beforeId, pageSize));

    /// <summary>
    /// 查询一页操作日志。
    /// </summary>
    /// <param name="service">日志查询。</param>
    /// <param name="from">起始时间（含）。</param>
    /// <param name="to">结束时间（不含）。</param>
    /// <param name="userId">操作人 ID。</param>
    /// <param name="type">操作类型。</param>
    /// <param name="q">在对象、说明、操作人里模糊查找。</param>
    /// <param name="beforeId">翻页：上一页最后一条的 ID。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <returns>日志，新的在前。</returns>
    private static Task<IReadOnlyList<OperationLogDto>> SearchOperationLogsAsync(
        ILogAppService service,
        DateTime? from,
        DateTime? to,
        int? userId,
        string? type,
        string? q,
        long? beforeId,
        int? pageSize) =>
        service.SearchOperationLogsAsync(new OperationLogSearchInput(from, to, userId, type, q, beforeId, pageSize));
}
