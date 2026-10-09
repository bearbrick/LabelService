using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using ZXing;
using ZXing.Common;
using ZXing.Datamatrix.Encoder;
using ZXing.PDF417.Internal;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 二维码编码：QR、DataMatrix、PDF417，用 ZXing.Net 编码出模块矩阵（不带空白区）。绘制由 <see cref="QrCodePainter"/> 按打印点完成。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>内容含中文等非 ASCII 字符时按 UTF-8 编码并写入 ECI 标识，扫码枪、手机都能正确识别；纯 ASCII 内容不加 ECI，兼容老设备。</item>
/// <item>DataMatrix 固定用正方形符号；PDF417 每行高 3 个模块，和设计器（bwip-js）一致。</item>
/// <item>ecLevel 只对 QR 生效。</item>
/// </list>
/// </remarks>
internal static class MatrixSymbols
{
    /// <summary>
    /// 编码（带缓存）。内容为空或超出容量时抛 BARCODE_INVALID。
    /// </summary>
    public static EncodedSymbol Encode(TwoDimensionalSymbology symbology, ErrorCorrectionLevel ecLevel, string content, string elementId)
    {
        var kind = symbology == TwoDimensionalSymbology.Qr ? $"2d:{symbology}:{ecLevel}" : "2d:" + symbology;
        return SymbolCache.GetOrAdd(kind, content, value => EncodeUncached(symbology, ecLevel, value, elementId));
    }

    /// <summary>
    /// 码制的显示名，用在错误消息里。
    /// </summary>
    public static string Name(TwoDimensionalSymbology symbology) => symbology switch
    {
        TwoDimensionalSymbology.Qr => "QR 码",
        TwoDimensionalSymbology.DataMatrix => "DataMatrix",
        TwoDimensionalSymbology.Pdf417 => "PDF417",
        _ => symbology.ToString(),
    };

    private static EncodedSymbol EncodeUncached(TwoDimensionalSymbology symbology, ErrorCorrectionLevel ecLevel, string content, string elementId)
    {
        LabelRenderException Invalid(string message) =>
            new(ErrorCodes.BarcodeInvalid, $"元素 {elementId} 的{Name(symbology)}内容无法编码：{message}", elementId);

        if (content.Length == 0)
        {
            throw Invalid("内容为空");
        }

        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 };
        if (content.Any(ch => ch >= 128))
        {
            hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
        }

        var format = symbology switch
        {
            TwoDimensionalSymbology.DataMatrix => BarcodeFormat.DATA_MATRIX,
            TwoDimensionalSymbology.Pdf417 => BarcodeFormat.PDF_417,
            _ => BarcodeFormat.QR_CODE,
        };
        switch (symbology)
        {
            case TwoDimensionalSymbology.Qr:
                hints[EncodeHintType.ERROR_CORRECTION] = ecLevel switch
                {
                    ErrorCorrectionLevel.L => ZXing.QrCode.Internal.ErrorCorrectionLevel.L,
                    ErrorCorrectionLevel.Q => ZXing.QrCode.Internal.ErrorCorrectionLevel.Q,
                    ErrorCorrectionLevel.H => ZXing.QrCode.Internal.ErrorCorrectionLevel.H,
                    _ => ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
                };
                break;
            case TwoDimensionalSymbology.DataMatrix:
                hints[EncodeHintType.DATA_MATRIX_SHAPE] = SymbolShapeHint.FORCE_SQUARE;
                break;
            case TwoDimensionalSymbology.Pdf417:
                hints[EncodeHintType.PDF417_ASPECT_RATIO] = PDF417AspectRatio.A3;
                break;
        }

        BitMatrix matrix;
        try
        {
            matrix = new MultiFormatWriter().encode(content, format, 0, 0, hints);
        }
        catch (Exception ex) when (ex is ArgumentException or WriterException or InvalidOperationException)
        {
            throw Invalid(ex.Message.Contains("too big", StringComparison.OrdinalIgnoreCase) ? "内容太长，超出容量" : ex.Message);
        }

        var modules = new bool[matrix.Width * matrix.Height];
        for (var y = 0; y < matrix.Height; y++)
        {
            for (var x = 0; x < matrix.Width; x++)
            {
                modules[y * matrix.Width + x] = matrix[x, y];
            }
        }

        return new EncodedSymbol(matrix.Width, matrix.Height, modules, content);
    }
}
