namespace LabelService.Contracts.Protocol;

/// <summary>
/// 输出格式。
/// </summary>
public enum LabelFormat
{
    /// <summary>矢量 PDF，一张标签一页。</summary>
    Pdf,

    /// <summary>按 DPI 栅格化的 PNG。</summary>
    Png,

    /// <summary>整张栅格化成黑白位图，用 ^GFA 输出的 ZPL。</summary>
    Zpl,
}

/// <summary>
/// 批量输出方式。
/// </summary>
public enum BatchOutput
{
    /// <summary>合并成一个文件：PDF 多页、ZPL 拼接。</summary>
    Merge,

    /// <summary>每张一个文件打包成 ZIP。PNG 只能用这种方式。</summary>
    Zip,
}

/// <summary>
/// 格式字符串、Content-Type 和扩展名之间的换算。请求里的格式按字符串接收，非法值返回 FORMAT_INVALID。
/// </summary>
public static class LabelFormats
{
    /// <summary>ZIP 包的 Content-Type。</summary>
    public const string ZipContentType = "application/zip";

    /// <summary>
    /// 解析请求里的 format，忽略大小写。
    /// </summary>
    /// <param name="value">请求里的字符串。</param>
    /// <param name="format">解析结果。</param>
    /// <returns>是否是 pdf、png、zpl 之一。</returns>
    public static bool TryParse(string? value, out LabelFormat format)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "pdf":
                format = LabelFormat.Pdf;
                return true;
            case "png":
                format = LabelFormat.Png;
                return true;
            case "zpl":
                format = LabelFormat.Zpl;
                return true;
            default:
                format = default;
                return false;
        }
    }

    /// <summary>
    /// 解析批量请求里的 output，忽略大小写；不传时按 merge。
    /// </summary>
    /// <param name="value">请求里的字符串。</param>
    /// <param name="output">解析结果。</param>
    /// <returns>是否是 merge、zip 之一或为空。</returns>
    public static bool TryParseOutput(string? value, out BatchOutput output)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null:
            case "":
            case "merge":
                output = BatchOutput.Merge;
                return true;
            case "zip":
                output = BatchOutput.Zip;
                return true;
            default:
                output = default;
                return false;
        }
    }

    /// <summary>
    /// 输出格式对应的 Content-Type。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <returns>Content-Type 字符串。</returns>
    public static string ContentType(LabelFormat format) => format switch
    {
        LabelFormat.Pdf => "application/pdf",
        LabelFormat.Png => "image/png",
        LabelFormat.Zpl => "text/plain; charset=utf-8",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    /// <summary>
    /// 输出格式对应的文件扩展名（不含点）。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <returns>pdf、png 或 zpl。</returns>
    public static string Extension(LabelFormat format) => format switch
    {
        LabelFormat.Pdf => "pdf",
        LabelFormat.Png => "png",
        LabelFormat.Zpl => "zpl",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}
