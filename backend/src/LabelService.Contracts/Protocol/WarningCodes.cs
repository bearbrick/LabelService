namespace LabelService.Contracts.Protocol;

/// <summary>
/// 渲染成功但需要提醒调用方的情况，写在 X-Label-Warnings 响应头里。
/// </summary>
public static class WarningCodes
{
    /// <summary>模板引用的字体不存在，已降级到思源黑体。detail 是原字体名。</summary>
    public const string FontFallback = "FONT_FALLBACK";

    /// <summary>文本超出元素框被裁掉。detail 是元素 ID。</summary>
    public const string TextClipped = "TEXT_CLIPPED";

    /// <summary>绑定引用了模板没有定义的字段，按空值处理。detail 是字段 key。</summary>
    public const string UnknownBinding = "BINDING_UNKNOWN";
}
