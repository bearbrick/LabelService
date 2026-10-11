namespace LabelService.Rendering;

/// <summary>
/// 因为数据或模板内容无法渲染（如条码内容不符合码制）而失败。服务端按 <see cref="ErrorCode"/> 返回 400。
/// </summary>
/// <remarks>
/// 只用于调用方能修正的问题。程序缺陷不要包装成它，让它以 RENDER_FAILED 暴露出来。
/// </remarks>
public sealed class LabelRenderException : Exception
{
    /// <summary>
    /// 创建异常。
    /// </summary>
    /// <param name="errorCode">错误码，见 <see cref="LabelService.Contracts.Protocol.ErrorCodes"/>。</param>
    /// <param name="message">给调用方看的中文说明。</param>
    /// <param name="elementId">出错的元素 ID。</param>
    public LabelRenderException(string errorCode, string message, string? elementId = null)
        : base(message)
    {
        ErrorCode = errorCode;
        ElementId = elementId;
    }

    /// <summary>
    /// 错误码。
    /// </summary>
    public string ErrorCode
    {
        get;
    }

    /// <summary>
    /// 出错的元素 ID。
    /// </summary>
    public string? ElementId
    {
        get;
    }

    /// <summary>
    /// 批量时出错的条目序号（从 0 开始）；单张为 null。
    /// </summary>
    public int? LabelIndex
    {
        get; private init;
    }

    /// <summary>
    /// 标上批量条目序号，提示文字前加“第 n 条：”。
    /// </summary>
    /// <param name="index">条目序号，从 0 开始。</param>
    /// <returns>带序号的新异常（错误码不变）。</returns>
    public LabelRenderException ForLabel(int index) =>
        new(ErrorCode, $"第 {index + 1} 条：{Message}", ElementId)
        {
            LabelIndex = index
        };
}
