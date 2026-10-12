namespace LabelService.Domain.Logs;

/// <summary>
/// 表 op_log（日志库）：管理后台的操作日志。登录、退出和所有写操作各记一条（ADR 0006）。
/// </summary>
public sealed class OperationLogEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public long Id
    {
        get; set;
    }

    /// <summary>
    /// 操作人 ID；登录失败时可能为空。
    /// </summary>
    public int? UserId
    {
        get; set;
    }

    /// <summary>
    /// 操作人姓名（登录失败时是尝试登录的账号）。
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    /// 操作类型，如 登录、发布模板、调用方。
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// 操作对象，如“BOX_LABEL 成品箱标”。
    /// </summary>
    public string Target { get; set; } = "";

    /// <summary>
    /// 说明。
    /// </summary>
    public string Detail { get; set; } = "";

    /// <summary>
    /// 来源 IP。
    /// </summary>
    public string Ip { get; set; } = "";

    /// <summary>
    /// 时间（UTC）。
    /// </summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}
