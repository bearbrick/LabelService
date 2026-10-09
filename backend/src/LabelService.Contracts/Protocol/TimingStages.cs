namespace LabelService.Contracts.Protocol;

/// <summary>
/// Server-Timing 头和 label_render_stage_ms 指标里的阶段名，顺序就是处理顺序。
/// </summary>
/// <remarks>
/// 示例：<c>auth;dur=0.3, tpl;dur=0.1, validate;dur=0.6, layout;dur=4.2, draw;dur=18.5, encode;dur=21.7, total;dur=46.1</c>。
/// 单张预算见 docs/performance.md。
/// </remarks>
public static class TimingStages
{
    /// <summary>API Key 校验。</summary>
    public const string Auth = "auth";

    /// <summary>取模板（内存缓存）。</summary>
    public const string Template = "tpl";

    /// <summary>数据校验和类型转换。</summary>
    public const string Validate = "validate";

    /// <summary>替换绑定、文本排版、条码编码。</summary>
    public const string Layout = "layout";

    /// <summary>SkiaSharp 绘制。</summary>
    public const string Draw = "draw";

    /// <summary>输出编码（PNG、PDF、ZPL）。</summary>
    public const string Encode = "encode";

    /// <summary>从收到请求到开始写响应的总耗时。</summary>
    public const string Total = "total";

    /// <summary>
    /// 全部阶段，按处理顺序。
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Auth, Template, Validate, Layout, Draw, Encode, Total];
}
