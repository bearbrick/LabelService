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
    /// 编码（带缓存）。
    /// </summary>
    /// <param name="symbology">码制。</param>
    /// <param name="ecLevel">纠错级别（只对 QR 生效）。</param>
    /// <param name="content">已替换绑定的内容。</param>
    /// <param name="elementId">元素 ID（错误消息里用）。</param>
    /// <returns>模块矩阵。</returns>
    /// <exception cref="LabelRenderException">内容为空或超出容量（BARCODE_INVALID）。</exception>
    public static EncodedSymbol Encode(TwoDimensionalSymbology symbology, ErrorCorrectionLevel ecLevel, string content, string elementId)
    {
        // 缓存键里带上码制（QR 还带纠错级别），同样的内容不同码制不会撞上。
        var kind = symbology == TwoDimensionalSymbology.Qr ? $"2d:{symbology}:{ecLevel}" : "2d:" + symbology;
        return SymbolCache.GetOrAdd(kind, content, value => EncodeUncached(symbology, ecLevel, value, elementId));
    }

    /// <summary>
    /// 码制的显示名，用在错误消息里。
    /// </summary>
    /// <param name="symbology">码制。</param>
    /// <returns>如 QR 码、PDF417。</returns>
    public static string Name(TwoDimensionalSymbology symbology) => symbology switch
    {
        TwoDimensionalSymbology.Qr => "QR 码",
        TwoDimensionalSymbology.DataMatrix => "DataMatrix",
        TwoDimensionalSymbology.Pdf417 => "PDF417",
        _ => symbology.ToString(),
    };

    /// <summary>
    /// 编码（不查缓存）。
    /// </summary>
    /// <param name="symbology">码制。</param>
    /// <param name="ecLevel">纠错级别。</param>
    /// <param name="content">内容。</param>
    /// <param name="elementId">元素 ID。</param>
    /// <returns>模块矩阵，按行存放。</returns>
    /// <exception cref="LabelRenderException">内容为空或超出容量。</exception>
    private static EncodedSymbol EncodeUncached(TwoDimensionalSymbology symbology, ErrorCorrectionLevel ecLevel, string content, string elementId)
    {
        LabelRenderException Invalid(string message) =>
            new(ErrorCodes.BarcodeInvalid, $"元素 {elementId} 的{Name(symbology)}内容无法编码：{message}", elementId);

        if (content.Length == 0)
        {
            throw Invalid("内容为空");
        }

        // 有非 ASCII 字符时指定 UTF-8（ZXing 会写入 ECI），纯 ASCII 不加，兼容不认 ECI 的老扫码枪。
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

                // 混合模式最小编码（数字、字母数字、字节分段），和设计器的 bwip-js 一样，同样的内容选出同样的版本（尺寸）。
                hints[EncodeHintType.QR_COMPACT] = true;
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

        // 矩阵拍平成一维数组，按行存放。
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
