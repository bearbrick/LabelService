using LabelService.Application.Identity;
using LabelService.Application.Logs.Dtos;
using LabelService.Domain.Logs;
using LabelService.Domain.Templates;

namespace LabelService.Application.Logs;

/// <summary>
/// 调用日志、操作日志的应用服务实现。
/// </summary>
/// <param name="renderLogs">调用日志仓储。</param>
/// <param name="operationLogs">操作日志仓储。</param>
/// <param name="templates">模板仓储（按业务范围算出能看的模板编码）。</param>
/// <param name="currentAdmin">当前用户。</param>
public sealed class LogAppService(
    IRenderLogRepository renderLogs,
    IOperationLogRepository operationLogs,
    ITemplateRepository templates,
    ICurrentAdmin currentAdmin) : ILogAppService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RenderLogDto>> SearchRenderLogsAsync(RenderLogSearchInput input)
    {
        var admin = currentAdmin.Principal;

        // 业务范围不是全部时，只看自己范围里的模板的日志。
        IReadOnlyCollection<string>? scope = admin.AllBusinesses
            ? null
            : (await templates.ListAsync()).Where(item => admin.InScope(item.BusinessId)).Select(item => item.Code).ToArray();
        var logs = await renderLogs.SearchAsync(new RenderLogFilter(
            From: input.From?.ToUniversalTime(),
            To: input.To?.ToUniversalTime(),
            TemplateCode: string.IsNullOrWhiteSpace(input.TemplateCode) ? null : input.TemplateCode,
            ClientId: input.ClientId,
            Status: string.IsNullOrWhiteSpace(input.Status) ? null : input.Status,
            Before: input.BeforeTime is { } time && input.BeforeId is { } id ? (time.ToUniversalTime(), id) : null,
            PageSize: input.PageSize ?? 50,
            TemplateCodes: scope,
            Search: input.Search));
        return logs.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperationLogDto>> SearchOperationLogsAsync(OperationLogSearchInput input)
    {
        var logs = await operationLogs.SearchAsync(new OperationLogFilter(
            From: input.From?.ToUniversalTime(),
            To: input.To?.ToUniversalTime(),
            UserId: input.UserId,
            Type: input.Type,
            Search: input.Search,
            BeforeId: input.BeforeId,
            PageSize: input.PageSize ?? 50));
        return logs.Select(ToDto).ToList();
    }

    /// <summary>
    /// 转成接口返回的操作日志。
    /// </summary>
    /// <param name="log">操作日志实体。</param>
    /// <returns>操作日志 DTO。</returns>
    internal static OperationLogDto ToDto(OperationLogEntity log) =>
        new(log.Id, log.UserId, log.UserName, log.Type, log.Target, log.Detail, log.Ip, log.CreatedAt);

    /// <summary>
    /// 转成接口返回的调用日志。
    /// </summary>
    /// <param name="log">调用日志实体。</param>
    /// <returns>调用日志 DTO。</returns>
    private static RenderLogDto ToDto(RenderLogEntity log) => new(
        log.Id,
        log.RequestId,
        log.TraceId,
        log.Ip,
        log.Endpoint,
        log.ClientId,
        log.ClientName,
        log.TemplateCode,
        log.VersionNo,
        log.Format,
        log.LayoutCode,
        log.ItemCount,
        log.DurationMs,
        log.StageMsJson,
        log.IsSlow,
        log.Status,
        log.ErrorCode,
        log.ErrorMessage,
        log.CreatedAt);
}
