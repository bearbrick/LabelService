namespace LabelService.Domain.Shared;

/// <summary>
/// 操作者能修正的问题：编码不合法、名称重复、没有草稿、检查不通过、并发冲突等。
/// </summary>
/// <remarks>
/// 消息直接给人看（中文）；接口层按 <see cref="Code"/> 转成 HTTP 状态码和错误响应 <c>{ code, message, details }</c>。
/// 程序缺陷不要包装成这个异常，让它以 500 暴露（AGENTS.md R6）。
/// </remarks>
public class UserFriendlyException : Exception
{
    /// <summary>
    /// 默认错误码：请求的内容不合法，对应 HTTP 400。
    /// </summary>
    public const string InvalidCode = "INVALID";

    /// <summary>
    /// 创建一个操作者能修正的问题。
    /// </summary>
    /// <param name="message">给人看的说明。</param>
    /// <param name="details">逐条问题，如发布检查不通过的全部原因；没有时为空。</param>
    /// <param name="innerException">引起这个问题的异常，如数据库的并发冲突。</param>
    /// <param name="code">错误码，默认 <see cref="InvalidCode"/>。</param>
    public UserFriendlyException(string message, IReadOnlyList<string>? details = null, Exception? innerException = null, string code = InvalidCode)
        : base(message, innerException)
    {
        Details = details ?? [];
        Code = code;
    }

    /// <summary>
    /// 错误码，如 INVALID、NOT_FOUND、FORBIDDEN。
    /// </summary>
    public string Code
    {
        get;
    }

    /// <summary>
    /// 全部问题，检查不通过时逐条列出。
    /// </summary>
    public IReadOnlyList<string> Details
    {
        get;
    }
}
