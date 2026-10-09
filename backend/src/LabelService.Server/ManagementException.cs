namespace LabelService.Server;

/// <summary>
/// 管理操作失败：操作者能修正的问题（编码不合法、没有草稿、检查不通过、并发冲突、名称重复等）。管理接口转成 400 / 409。
/// </summary>
internal sealed class ManagementException(string message, IReadOnlyList<string>? details = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// 全部问题，检查不通过时逐条列出。
    /// </summary>
    public IReadOnlyList<string> Details { get; } = details ?? [];
}
