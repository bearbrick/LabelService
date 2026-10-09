using System.Text.Json.Serialization;

namespace LabelService.Contracts.Templates;

/// <summary>
/// 文字样式。属性为 null 表示取默认值，设计器不必把默认值写进 JSON。
/// </summary>
public sealed class TextStyle
{
    /// <summary>默认字体：思源黑体（SIL OFL，可随服务分发）。</summary>
    public const string DefaultFontFamily = "SourceHanSansSC";

    /// <summary>默认字号（pt）。</summary>
    public const double DefaultFontSize = 10;

    /// <summary>默认颜色。</summary>
    public const string DefaultColor = "#000000";

    /// <summary>默认行高倍数。</summary>
    public const double DefaultLineHeight = 1.2;

    /// <summary>
    /// 字体名，按服务端 fonts/ 目录里的字体匹配；找不到时降级到默认字体并给出 FONT_FALLBACK 警告。
    /// </summary>
    public string? FontFamily
    {
        get; set;
    }

    /// <summary>
    /// 字号（pt），1pt ≈ 0.353mm。
    /// </summary>
    public double? FontSize
    {
        get; set;
    }

    /// <summary>粗体。</summary>
    public bool? Bold
    {
        get; set;
    }

    /// <summary>斜体。</summary>
    public bool? Italic
    {
        get; set;
    }

    /// <summary>下划线。</summary>
    public bool? Underline
    {
        get; set;
    }

    /// <summary>
    /// 颜色 #RRGGBB。
    /// </summary>
    public string? Color
    {
        get; set;
    }

    /// <summary>
    /// 水平对齐，默认左对齐。
    /// </summary>
    public TextAlign? Align
    {
        get; set;
    }

    /// <summary>
    /// 垂直对齐，默认居中。
    /// </summary>
    public VerticalAlign? VAlign
    {
        get; set;
    }

    /// <summary>
    /// 放不下时的处理，默认 none（单行裁切）。
    /// </summary>
    public TextFit? Fit
    {
        get; set;
    }

    /// <summary>
    /// fit 为 shrink 时最小缩到的字号（pt）。
    /// </summary>
    public double? MinFontSize
    {
        get; set;
    }

    /// <summary>
    /// 行高倍数：行高 = 字号 × lineHeight。
    /// </summary>
    public double? LineHeight
    {
        get; set;
    }

    /// <summary>
    /// 字间距（mm）。
    /// </summary>
    public double? LetterSpacing
    {
        get; set;
    }
}

/// <summary>
/// 线型。
/// </summary>
public sealed class StrokeStyle
{
    /// <summary>默认线宽（mm）。</summary>
    public const double DefaultWidth = 0.3;

    /// <summary>
    /// 线宽（mm）。
    /// </summary>
    public double Width { get; set; } = DefaultWidth;

    /// <summary>
    /// 虚线的实段、空段长度（mm），如 [1, 0.5]；不设为实线。
    /// </summary>
    public double[]? Dash
    {
        get; set;
    }

    /// <summary>
    /// 颜色 #RRGGBB，默认 #000000。
    /// </summary>
    public string? Color
    {
        get; set;
    }
}

/// <summary>水平对齐。</summary>
public enum TextAlign
{
    /// <summary>左对齐。</summary>
    Left,

    /// <summary>居中。</summary>
    Center,

    /// <summary>右对齐。</summary>
    Right,
}

/// <summary>垂直对齐。</summary>
public enum VerticalAlign
{
    /// <summary>顶部。</summary>
    Top,

    /// <summary>居中。</summary>
    Middle,

    /// <summary>底部。</summary>
    Bottom,
}

/// <summary>文本放不下时的处理方式。</summary>
public enum TextFit
{
    /// <summary>单行，超出框的部分裁掉。</summary>
    None,

    /// <summary>在框宽内自动换行（中文按字、英文按单词），超出框高的部分裁掉。</summary>
    Wrap,

    /// <summary>单行，放不下时缩小字号，最小到 minFontSize，还放不下就裁掉。</summary>
    Shrink,
}

/// <summary>一维码码制。</summary>
public enum BarcodeSymbology
{
    /// <summary>Code 128。</summary>
    Code128,

    /// <summary>Code 39。</summary>
    Code39,

    /// <summary>EAN-13，内容 12 或 13 位数字。</summary>
    Ean13,

    /// <summary>EAN-8，内容 7 或 8 位数字。</summary>
    Ean8,

    /// <summary>UPC-A，内容 11 或 12 位数字。</summary>
    Upca,

    /// <summary>ITF-14，内容 13 或 14 位数字。</summary>
    Itf14,

    /// <summary>GS1-128，内容用 (AI) 写法，如 (01)06901234567892(10)A01。</summary>
    [JsonStringEnumMemberName("gs1-128")]
    Gs1128,
}

/// <summary>二维码码制。</summary>
public enum TwoDimensionalSymbology
{
    /// <summary>QR Code。</summary>
    Qr,

    /// <summary>Data Matrix。</summary>
    [JsonStringEnumMemberName("datamatrix")]
    DataMatrix,

    /// <summary>PDF417。</summary>
    Pdf417,
}

/// <summary>QR 纠错级别。</summary>
public enum ErrorCorrectionLevel
{
    /// <summary>约 7%。</summary>
    [JsonStringEnumMemberName("L")]
    L,

    /// <summary>约 15%。</summary>
    [JsonStringEnumMemberName("M")]
    M,

    /// <summary>约 25%。</summary>
    [JsonStringEnumMemberName("Q")]
    Q,

    /// <summary>约 30%。</summary>
    [JsonStringEnumMemberName("H")]
    H,
}

/// <summary>一维码文字位置。</summary>
public enum BarcodeTextPosition
{
    /// <summary>条码下方。</summary>
    Bottom,

    /// <summary>条码上方。</summary>
    Top,
}

/// <summary>图片适应方式。</summary>
public enum ImageFit
{
    /// <summary>完整放进框内，保持比例。</summary>
    Contain,

    /// <summary>铺满框，保持比例，超出部分裁掉。</summary>
    Cover,

    /// <summary>拉伸铺满，不保持比例。</summary>
    Stretch,
}
