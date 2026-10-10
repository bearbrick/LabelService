namespace LabelService.Application.Rendering.Dtos;

/// <summary>
/// 管理接口里渲染出来的文件（服务端预览、接口调试、版式预览、测试页）。接口层据此写响应体和响应头，值为 null 的响应头不写。
/// </summary>
/// <param name="Content">文件内容。</param>
/// <param name="ContentType">MIME 类型，如 image/png。</param>
/// <param name="FileName">下载时的文件名；直接显示的（如预览图）为 null。</param>
/// <param name="ServerTiming">各阶段耗时（响应头 Server-Timing）。</param>
/// <param name="Warnings">渲染提示，如文字缩小、被截断（响应头 X-Label-Warnings，没有提示时不写）。</param>
public sealed record RenderedLabelDto(
    ReadOnlyMemory<byte> Content,
    string ContentType,
    string? FileName,
    string ServerTiming,
    IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// 实际使用的分辨率（响应头 X-Label-Dpi）。
    /// </summary>
    public int? Dpi
    {
        get;
        init;
    }

    /// <summary>
    /// 用的模板版本（响应头 X-Template-Version）。
    /// </summary>
    public int? TemplateVersion
    {
        get;
        init;
    }

    /// <summary>
    /// 标签张数（响应头 X-Label-Count）。
    /// </summary>
    public int? LabelCount
    {
        get;
        init;
    }

    /// <summary>
    /// 页数（响应头 X-Page-Count）。
    /// </summary>
    public int? PageCount
    {
        get;
        init;
    }
}
