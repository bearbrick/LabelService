using LabelService.Contracts.Protocol;

namespace LabelService.Application.OpenApi;

/// <summary>
/// 开放接口一次处理的结果：成功时有 <see cref="Value"/>，失败时有 <see cref="Error"/>。
/// </summary>
/// <remarks>
/// 开放接口的失败是常态（调用方传错数据），用返回值而不是异常表达，热路径上不抛异常（AGENTS.md R3）。
/// </remarks>
/// <typeparam name="T">成功时的结果类型。</typeparam>
public sealed class OpenApiOutcome<T>
    where T : class
{
    /// <summary>
    /// 由结果或错误创建（两者恰好一个不为 null）。
    /// </summary>
    /// <param name="value">成功时的结果。</param>
    /// <param name="error">失败时的错误。</param>
    private OpenApiOutcome(T? value, OpenApiError? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 成功时的结果；失败时为 null。
    /// </summary>
    public T? Value
    {
        get;
    }

    /// <summary>
    /// 失败时的错误；成功时为 null。
    /// </summary>
    public OpenApiError? Error
    {
        get;
    }

    /// <summary>
    /// 从结果隐式转换成成功。
    /// </summary>
    /// <param name="value">结果。</param>
    public static implicit operator OpenApiOutcome<T>(T value) => new(value, null);

    /// <summary>
    /// 从错误隐式转换成失败。
    /// </summary>
    /// <param name="error">错误。</param>
    public static implicit operator OpenApiOutcome<T>(OpenApiError error) => new(null, error);
}

/// <summary>
/// 开放接口的错误（接口层转成 <see cref="ErrorResponse"/>）。
/// </summary>
/// <param name="Code">错误码，见 <see cref="ErrorCodes"/>。</param>
/// <param name="Message">中文说明。</param>
/// <param name="Errors">逐条明细（字段错误、批量的条目序号）；没有时为 null。</param>
/// <param name="StatusCode">HTTP 状态码；null 时按错误码取（<see cref="ErrorCodes.StatusCodes"/>）。</param>
public sealed record OpenApiError(string Code, string Message, IReadOnlyList<ErrorDetail>? Errors = null, int? StatusCode = null)
{
    /// <summary>
    /// 开发期临时错误码：输出格式还没实现。不属于对外契约，1.0 前所有用到它的地方都要消失。
    /// </summary>
    public const string NotImplemented = "NOT_IMPLEMENTED";

    /// <summary>
    /// 是否要求调用方稍后重试（排队超时的 503，接口层加 Retry-After: 1）。
    /// </summary>
    public bool RetryLater
    {
        get;
        init;
    }

    /// <summary>
    /// 排队等渲染名额超时。
    /// </summary>
    /// <param name="timeoutSeconds">排队的超时时间，秒。</param>
    /// <returns>503 SERVICE_UNAVAILABLE，要求稍后重试。</returns>
    public static OpenApiError Busy(double timeoutSeconds) =>
        new(ErrorCodes.ServiceUnavailable, $"服务繁忙：排队超过 {timeoutSeconds:0.#} 秒，请稍后再试", StatusCode: 503)
        {
            RetryLater = true
        };
}

/// <summary>
/// 开放接口生成的文件。
/// </summary>
/// <param name="Content">文件内容。</param>
/// <param name="ContentType">Content-Type。</param>
/// <param name="Extension">下载文件名的扩展名。</param>
/// <param name="LabelCount">张数（含份数，响应头 X-Label-Count）。</param>
/// <param name="Dpi">分辨率（响应头 X-Label-Dpi）。</param>
/// <param name="Warnings">渲染提示（响应头 X-Label-Warnings）。</param>
/// <param name="TemplateVersion">模板版本（响应头 X-Template-Version）。</param>
/// <param name="PageCount">拼版时的页数（响应头 X-Page-Count）；不拼版时为 null，不写这个头。</param>
public sealed record RenderedFile(
    ReadOnlyMemory<byte> Content,
    string ContentType,
    string Extension,
    int LabelCount,
    int Dpi,
    IReadOnlyCollection<string> Warnings,
    int TemplateVersion,
    int? PageCount);
