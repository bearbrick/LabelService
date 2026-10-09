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
}
