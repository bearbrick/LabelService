using LabelService.Contracts.Protocol;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Rendering;

/// <summary>
/// 渲染结果。<see cref="Content"/> 直接写进响应流，不再复制。
/// </summary>
public sealed class RenderResult
{
    /// <summary>
    /// 文件内容。
    /// </summary>
    public required ReadOnlyMemory<byte> Content
    {
        get; init;
    }

    /// <summary>
    /// 输出格式。
    /// </summary>
    public required LabelFormat Format
    {
        get; init;
    }

    /// <summary>
    /// 文件里的标签张数（含份数）。
    /// </summary>
    public required int LabelCount
    {
        get; init;
    }

    /// <summary>
    /// 实际使用的分辨率。
    /// </summary>
    public required int Dpi
    {
        get; init;
    }

    /// <summary>
    /// 渲染警告，格式 <c>CODE:detail</c>，已去重。
    /// </summary>
    public required IReadOnlyList<string> Warnings
    {
        get; init;
    }

    /// <summary>
    /// 分阶段耗时（与 <see cref="RenderJob.Timings"/> 是同一个对象）。
    /// </summary>
    public required RenderTimings Timings
    {
        get; init;
    }

    /// <summary>
    /// Content-Type。
    /// </summary>
    public string ContentType => LabelFormats.ContentType(Format);
}
