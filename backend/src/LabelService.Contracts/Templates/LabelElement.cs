using System.Text.Json.Serialization;

namespace LabelService.Contracts.Templates;

/// <summary>
/// 画布上的一个对象。JSON 里用 type 区分具体类型；新增元素类型属于契约变更，见 AGENTS.md。
/// </summary>
/// <remarks>
/// 坐标原点在标签左上角，x 向右、y 向下，单位 mm。<see cref="Rotation"/> 按度、顺时针、绕元素中心旋转。
/// </remarks>
/// <param name="type">JSON 里的 type 值，由各派生类固定传入。</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(TextElement), ElementTypes.Text)]
[JsonDerivedType(typeof(BarcodeElement), ElementTypes.Barcode)]
[JsonDerivedType(typeof(QrCodeElement), ElementTypes.QrCode)]
[JsonDerivedType(typeof(ImageElement), ElementTypes.Image)]
[JsonDerivedType(typeof(IconElement), ElementTypes.Icon)]
[JsonDerivedType(typeof(LineElement), ElementTypes.Line)]
[JsonDerivedType(typeof(RectElement), ElementTypes.Rect)]
public abstract class LabelElement(string type)
{
    /// <summary>
    /// 元素 ID，模板内唯一，设计器生成。
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// JSON 里的 type 值，见 <see cref="ElementTypes"/>。
    /// </summary>
    [JsonIgnore]
    public string Type { get; } = type;

    /// <summary>
    /// 图层面板里显示的名字，可空。
    /// </summary>
    public string? Name
    {
        get; set;
    }

    /// <summary>
    /// 左上角 x（mm）。
    /// </summary>
    public double X
    {
        get; set;
    }

    /// <summary>
    /// 左上角 y（mm）。
    /// </summary>
    public double Y
    {
        get; set;
    }

    /// <summary>
    /// 宽度（mm）。竖线为 0。
    /// </summary>
    public double Width
    {
        get; set;
    }

    /// <summary>
    /// 高度（mm）。横线为 0。
    /// </summary>
    public double Height
    {
        get; set;
    }

    /// <summary>
    /// 旋转角度（度），顺时针，绕元素中心。条码只允许 0、90、180、270。
    /// </summary>
    public double Rotation
    {
        get; set;
    }

    /// <summary>
    /// 设计器里锁定，防止误拖。不影响渲染。
    /// </summary>
    public bool Locked
    {
        get; set;
    }

    /// <summary>
    /// 隐藏的元素不渲染。
    /// </summary>
    public bool Hidden
    {
        get; set;
    }
}

/// <summary>
/// 元素 type 取值。
/// </summary>
public static class ElementTypes
{
    /// <summary>文本。</summary>
    public const string Text = "text";

    /// <summary>一维码。</summary>
    public const string Barcode = "barcode";

    /// <summary>二维码（QR、DataMatrix、PDF417）。</summary>
    public const string QrCode = "qrcode";

    /// <summary>图片。</summary>
    public const string Image = "image";

    /// <summary>图标库里的 SVG 图标。</summary>
    public const string Icon = "icon";

    /// <summary>横线或竖线。</summary>
    public const string Line = "line";

    /// <summary>矩形。</summary>
    public const string Rect = "rect";

    /// <summary>
    /// 全部元素类型，按设计器元素库的顺序。
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Text, Barcode, QrCode, Image, Icon, Line, Rect];
}
