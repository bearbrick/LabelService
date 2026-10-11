namespace LabelService.Application.Logs.Dtos;

/// <summary>
/// 一条调用日志（开放接口的一次调用）。
/// </summary>
/// <param name="Id">日志 ID。</param>
/// <param name="RequestId">调用方传的 X-Request-Id。</param>
/// <param name="TraceId">链路 ID，排查问题时凭它找日志。</param>
/// <param name="Ip">来源 IP。</param>
/// <param name="Endpoint">接口：render（单张）或 batch（批量）。</param>
/// <param name="ClientId">调用方 ID；配置文件里的调用方为 null。</param>
/// <param name="ClientName">调用方名称。</param>
/// <param name="TemplateCode">模板编码。</param>
/// <param name="VersionNo">用的模板版本；模板不存在时为 null。</param>
/// <param name="Format">输出格式：png、pdf、zpl。</param>
/// <param name="LayoutCode">拼版版式编码；不拼版时为 null。</param>
/// <param name="ItemCount">标签张数。</param>
/// <param name="DurationMs">服务端处理耗时，毫秒。</param>
/// <param name="StageMsJson">各阶段耗时（JSON，键为阶段名）。</param>
/// <param name="IsSlow">是否慢调用（单张超过 100 ms）。</param>
/// <param name="Status">结果：ok、error、aborted。</param>
/// <param name="ErrorCode">错误码；成功时为 null。</param>
/// <param name="ErrorMessage">错误说明；成功时为 null。</param>
/// <param name="CreatedAt">调用时间（UTC）。</param>
public sealed record RenderLogDto(
    long Id,
    string RequestId,
    string TraceId,
    string Ip,
    string Endpoint,
    int? ClientId,
    string ClientName,
    string TemplateCode,
    int? VersionNo,
    string Format,
    string? LayoutCode,
    int ItemCount,
    double DurationMs,
    string StageMsJson,
    bool IsSlow,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime CreatedAt);

/// <summary>
/// 一条操作日志（后台的登录、退出和写操作）。
/// </summary>
/// <param name="Id">日志 ID。</param>
/// <param name="UserId">操作人 ID；登录失败、账号不存在时为 null。</param>
/// <param name="UserName">操作人姓名，或尝试登录的账号。</param>
/// <param name="Type">操作类型，如“发布模板”。</param>
/// <param name="Target">操作对象，如“BOX_LABEL 成品箱标”。</param>
/// <param name="Detail">说明。</param>
/// <param name="Ip">来源 IP。</param>
/// <param name="CreatedAt">操作时间（UTC）。</param>
public sealed record OperationLogDto(long Id, int? UserId, string UserName, string Type, string Target, string Detail, string Ip, DateTime CreatedAt);

/// <summary>
/// 调用日志的查询条件。时间按 UTC 比较（带时区的时间先换算）。
/// </summary>
/// <param name="From">起始时间（含）。</param>
/// <param name="To">结束时间（不含）。</param>
/// <param name="TemplateCode">模板编码。</param>
/// <param name="ClientId">调用方 ID。</param>
/// <param name="Status">结果：ok、error、aborted。</param>
/// <param name="Search">按链路 ID、X-Request-Id、模板编码或错误码精确查找。</param>
/// <param name="BeforeTime">翻页：上一页最后一条的时间。</param>
/// <param name="BeforeId">翻页：上一页最后一条的 ID。</param>
/// <param name="PageSize">每页条数，默认 50，最多 200。</param>
public sealed record RenderLogSearchInput(
    DateTime? From,
    DateTime? To,
    string? TemplateCode,
    int? ClientId,
    string? Status,
    string? Search,
    DateTime? BeforeTime,
    long? BeforeId,
    int? PageSize);

/// <summary>
/// 操作日志的查询条件。
/// </summary>
/// <param name="From">起始时间（含）。</param>
/// <param name="To">结束时间（不含）。</param>
/// <param name="UserId">操作人 ID。</param>
/// <param name="Type">操作类型。</param>
/// <param name="Search">在对象、说明、操作人里模糊查找。</param>
/// <param name="BeforeId">翻页：上一页最后一条的 ID。</param>
/// <param name="PageSize">每页条数，默认 50，最多 200。</param>
public sealed record OperationLogSearchInput(DateTime? From, DateTime? To, int? UserId, string? Type, string? Search, long? BeforeId, int? PageSize);
