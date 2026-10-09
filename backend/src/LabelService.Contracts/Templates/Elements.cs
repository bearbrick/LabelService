namespace LabelService.Contracts.Templates;

/// <summary>
/// 文本元素。固定文字和字段可以混写，如 <c>批次：{{batchNo}}</c>。
/// </summary>
public sealed class TextElement() : LabelElement(ElementTypes.Text)
{
    /// <summary>
    /// 文本内容，可含绑定。
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// 文字样式，省略的属性取 <see cref="TextStyle"/> 里写明的默认值。
    /// </summary>
    public TextStyle? Style
    {
        get; set;
    }
}

/// <summary>
/// 一维码元素。模块宽度对齐打印点，见 docs/contracts.md 的“一维码尺寸规则”。
/// </summary>
public sealed class BarcodeElement() : LabelElement(ElementTypes.Barcode)
{
    /// <summary>
    /// 码制，默认 Code128。
    /// </summary>
    public BarcodeSymbology Symbology { get; set; } = BarcodeSymbology.Code128;

    /// <summary>
    /// 条码内容，可含绑定。
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// 条码宽度小于框宽时在框内的水平位置，默认左对齐。
    /// </summary>
    public TextAlign Align { get; set; } = TextAlign.Left;

    /// <summary>
    /// 是否显示人眼可读文字，默认显示。
    /// </summary>
    public bool ShowText { get; set; } = true;

    /// <summary>
    /// 文字在条码上方还是下方。
    /// </summary>
    public BarcodeTextPosition TextPosition { get; set; } = BarcodeTextPosition.Bottom;

    /// <summary>
    /// 人眼可读文字的样式。
    /// </summary>
    public TextStyle? TextStyle
    {
        get; set;
    }

    /// <summary>
    /// 最小模块宽度（打印点），默认 2。按框宽算出的模块宽度小于它时返回 BARCODE_TOO_DENSE。
    /// </summary>
    public int MinModuleDots { get; set; } = 2;

    /// <summary>
    /// 框内左右留白（mm），默认 0，即静区由框外留白保证。
    /// </summary>
    public double? QuietZone
    {
        get; set;
    }
}

/// <summary>
/// 二维码元素，内容可以拼接多个字段，如 <c>{{partNo}}|{{batchNo}}</c>。
/// </summary>
public sealed class QrCodeElement() : LabelElement(ElementTypes.QrCode)
{
    /// <summary>
    /// 码制，默认 QR。
    /// </summary>
    public TwoDimensionalSymbology Symbology { get; set; } = TwoDimensionalSymbology.Qr;

    /// <summary>
    /// 内容，可含绑定。
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// 纠错级别，仅 QR 有效，默认 M。
    /// </summary>
    public ErrorCorrectionLevel EcLevel { get; set; } = ErrorCorrectionLevel.M;

    /// <summary>
    /// 框内四周留白（mm），默认 0。
    /// </summary>
    public double? QuietZone
    {
        get; set;
    }
}

/// <summary>
/// 图片元素。
/// </summary>
public sealed class ImageElement() : LabelElement(ElementTypes.Image)
{
    /// <summary>
    /// 来源：<c>asset:素材ID</c> 引用素材库，或 <c>{{字段}}</c> 引用 image 类型字段。
    /// </summary>
    public string Src { get; set; } = "";

    /// <summary>
    /// 图片和框比例不一致时的处理方式，默认 contain。
    /// </summary>
    public ImageFit Fit { get; set; } = ImageFit.Contain;

    /// <summary>
    /// 是否转成黑白（抖动），热敏打印时建议开启。
    /// </summary>
    public bool Mono
    {
        get; set;
    }
}

/// <summary>
/// 图标元素，来自内置图标库（如 GB/T 191 包装储运图示标志）或上传的 SVG。
/// </summary>
public sealed class IconElement() : LabelElement(ElementTypes.Icon)
{
    /// <summary>
    /// 图标库路径，如 <c>gb191/fragile</c>。
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    /// 颜色 #RRGGBB，默认 #000000。
    /// </summary>
    public string? Color
    {
        get; set;
    }
}

/// <summary>
/// 线元素：height 为 0 是横线，width 为 0 是竖线。
/// </summary>
public sealed class LineElement() : LabelElement(ElementTypes.Line)
{
    /// <summary>
    /// 线型。
    /// </summary>
    public StrokeStyle? Stroke
    {
        get; set;
    }
}

/// <summary>
/// 矩形元素，用来画外框和分区。
/// </summary>
public sealed class RectElement() : LabelElement(ElementTypes.Rect)
{
    /// <summary>
    /// 边框，不设则没有边框。
    /// </summary>
    public StrokeStyle? Stroke
    {
        get; set;
    }

    /// <summary>
    /// 填充色 #RRGGBB，不设则不填充。
    /// </summary>
    public string? Fill
    {
        get; set;
    }

    /// <summary>
    /// 圆角半径（mm）。
    /// </summary>
    public double? Radius
    {
        get; set;
    }
}
