namespace LabelService.Client;

/// <summary>
/// 调用失败。服务端返回的错误和网络错误都转成它，按 <see cref="ErrorCode"/> 区分。
/// </summary>
public sealed class LabelServiceException : Exception
{
    /// <summary>
    /// 创建异常。
    /// </summary>
    /// <param name="errorCode">错误码，见 <see cref="LabelErrorCodes"/>。</param>
    /// <param name="message">中文说明。</param>
    /// <param name="statusCode">HTTP 状态码；网络错误和超时为 null。</param>
    /// <param name="innerException">原始异常。</param>
    public LabelServiceException(string errorCode, string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    /// <summary>错误码，见 <see cref="LabelErrorCodes"/>。</summary>
    public string ErrorCode
    {
        get;
    }

    /// <summary>HTTP 状态码；网络错误和超时为 null。</summary>
    public int? StatusCode
    {
        get;
    }

    /// <summary>服务端链路 ID，反馈问题时提供。</summary>
    public string? TraceId
    {
        get; internal set;
    }

    /// <summary>本次调用的 X-Request-Id。</summary>
    public string? RequestId
    {
        get; internal set;
    }

    /// <summary>逐条、逐字段的错误明细。</summary>
    public IReadOnlyList<LabelFieldError> Errors { get; internal set; } = new LabelFieldError[0];

    /// <summary>
    /// 是否是暂时性错误（网络、超时、502/503/504）。SDK 已经重试过，调用方可以稍后再试。
    /// </summary>
    public bool IsTransient =>
        ErrorCode == LabelErrorCodes.NetworkError
        || ErrorCode == LabelErrorCodes.Timeout
        || ErrorCode == LabelErrorCodes.ServiceUnavailable;
}

/// <summary>
/// 一条错误明细。
/// </summary>
public sealed class LabelFieldError
{
    /// <summary>批量条目序号，从 0 开始；单张请求为 null。</summary>
    public int? Index
    {
        get; set;
    }

    /// <summary>字段 key 或请求参数名。</summary>
    public string? Field
    {
        get; set;
    }

    /// <summary>错误码。</summary>
    public string Code { get; set; } = "";

    /// <summary>中文说明。</summary>
    public string? Message
    {
        get; set;
    }
}

/// <summary>
/// 每次调用结束（成功或失败）时的信息，供调用方写日志。
/// </summary>
public sealed class CallCompletedEventArgs : EventArgs
{
    /// <summary>操作：Render、RenderBatch、GetSchema。</summary>
    public string Operation { get; internal set; } = "";

    /// <summary>模板编码。</summary>
    public string TemplateCode { get; internal set; } = "";

    /// <summary>最后一次尝试的 HTTP 状态码；网络错误和超时为 null。</summary>
    public int? StatusCode
    {
        get; internal set;
    }

    /// <summary>失败时的错误码；成功为 null。</summary>
    public string? ErrorCode
    {
        get; internal set;
    }

    /// <summary>含重试在内的总耗时。</summary>
    public TimeSpan Duration
    {
        get; internal set;
    }

    /// <summary>最后一次尝试的服务端耗时（Server-Timing total）。</summary>
    public TimeSpan? ServerDuration
    {
        get; internal set;
    }

    /// <summary>尝试次数，1 表示没有重试。</summary>
    public int Attempts
    {
        get; internal set;
    }

    /// <summary>本次调用的 X-Request-Id。</summary>
    public string RequestId { get; internal set; } = "";

    /// <summary>服务端链路 ID。</summary>
    public string? TraceId
    {
        get; internal set;
    }
}
