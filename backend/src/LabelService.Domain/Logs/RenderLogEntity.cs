namespace LabelService.Domain.Logs;

/// <summary>
/// 表 render_log（日志库）：开放接口的一次生成请求一条。不存业务数据。
/// </summary>
public sealed class RenderLogEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public long Id
    {
        get; set;
    }

    /// <summary>
    /// 调用方传的 X-Request-Id；没传时为空。
    /// </summary>
    public string RequestId { get; set; } = "";

    /// <summary>
    /// 服务端链路 ID（X-Trace-Id），调用方报问题时凭它查。
    /// </summary>
    public string TraceId { get; set; } = "";

    /// <summary>
    /// 调用方的来源 IP。
    /// </summary>
    public string Ip { get; set; } = "";

    /// <summary>
    /// 接口：render、batch。
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// 调用方 ID（api_client.id）；配置文件里的调用方和未识别的请求为 null。
    /// </summary>
    public int? ClientId
    {
        get; set;
    }

    /// <summary>
    /// 调用方名称，未识别时为 anonymous。调用方改名、删除后仍能看出是谁。
    /// </summary>
    public string ClientName { get; set; } = "";

    /// <summary>
    /// 模板编码。
    /// </summary>
    public string TemplateCode { get; set; } = "";

    /// <summary>
    /// 实际使用的模板版本；没走到取模板时为 null。
    /// </summary>
    public int? VersionNo
    {
        get; set;
    }

    /// <summary>
    /// 输出格式：pdf、png、zpl。
    /// </summary>
    public string Format { get; set; } = "";

    /// <summary>
    /// 拼版版式编码；不拼版时为 null。
    /// </summary>
    public string? LayoutCode
    {
        get; set;
    }

    /// <summary>
    /// 张数（含份数），失败时为 0。
    /// </summary>
    public int ItemCount
    {
        get; set;
    }

    /// <summary>
    /// 服务端总耗时（到写完最后一个字节），毫秒。
    /// </summary>
    public double DurationMs
    {
        get; set;
    }

    /// <summary>
    /// 各阶段耗时，如 {"auth":0.3,"layout":4.2}。
    /// </summary>
    public string StageMsJson { get; set; } = "{}";

    /// <summary>
    /// 是否慢调用（平均每张超过 100 ms）。
    /// </summary>
    public bool IsSlow
    {
        get; set;
    }

    /// <summary>
    /// 结果：ok、error、aborted。
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    /// 失败时的错误码。
    /// </summary>
    public string? ErrorCode
    {
        get; set;
    }

    /// <summary>
    /// 失败时给调用方的说明（截断到 512 字）。
    /// </summary>
    public string? ErrorMessage
    {
        get; set;
    }

    /// <summary>
    /// 请求时间（UTC）。
    /// </summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}
