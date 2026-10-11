using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Elements;
using LabelService.Rendering.Validation;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace LabelService.Backend.Checks;

/// <summary>
/// 二维码：QR、DataMatrix、PDF417 渲染后用 ZXing.Net 解码核对（含中文），ecLevel、quietZone、居中、模块对齐打印点、错误码。
/// </summary>
internal static class QrCodeChecks
{
    /// <summary>
    /// 运行二维码的全部检查：三种码制渲染后解码核对内容。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    public static void Run(string backendRoot, Action<string, bool> check)
    {
        var renderer = Fixtures.Renderer(backendRoot);
        var template = Fixtures.LoadTemplate(backendRoot);
        var values = FieldValidator.Validate(template.Fields, null, Fixtures.Data(Fixtures.BoxLabelData)).Values;
        foreach (var dpi in new[] { 203, 300 })
        {
            using var bitmap = Bitmap(renderer.Render(Job(template, dpi, values)));
            check($"示例箱标的二维码 {dpi}dpi 能解码，内容是拼接的三个字段", Decode(bitmap, BarcodeFormat.QR_CODE) == "3100-0123|20261008-A|C2610080001");
        }

        (TwoDimensionalSymbology Symbology, BarcodeFormat Format, string Value)[] cases =
        [
            (TwoDimensionalSymbology.Qr, BarcodeFormat.QR_CODE, "https://example.com/p?id=3100-0123&lot=20261008-A"),
            (TwoDimensionalSymbology.Qr, BarcodeFormat.QR_CODE, "品名：电源适配器 数量：50"),
            (TwoDimensionalSymbology.DataMatrix, BarcodeFormat.DATA_MATRIX, "3100-0123|C2610080001"),
            (TwoDimensionalSymbology.DataMatrix, BarcodeFormat.DATA_MATRIX, "电源适配器"),
            (TwoDimensionalSymbology.Pdf417, BarcodeFormat.PDF_417, "3100-0123|20261008-A|C2610080001"),
            (TwoDimensionalSymbology.Pdf417, BarcodeFormat.PDF_417, "电源适配器 3100-0123"),
        ];
        foreach (var dpi in new[] { 203, 300 })
        {
            var failed = new List<string>();
            foreach (var (symbology, format, value) in cases)
            {
                var element = new QrCodeElement { Id = "q", X = 10, Y = 10, Width = 40, Height = 40, Symbology = symbology, Value = value };
                if (symbology == TwoDimensionalSymbology.Pdf417)
                {
                    element.Width = 70;
                    element.Height = 25;
                }

                using var bitmap = Bitmap(renderer.Render(Job(Single(element), dpi)));
                var decoded = Decode(bitmap, format);
                if (decoded != value)
                {
                    failed.Add($"{MatrixSymbols.Name(symbology)} {value} → {decoded ?? "解不出"}");
                }
            }

            check($"QR、DataMatrix、PDF417 {dpi}dpi 都能解码回原内容，中文按 UTF-8" + (failed.Count > 0 ? "：" + string.Join("；", failed) : ""), failed.Count == 0);
        }

        var low = MatrixSymbols.Encode(TwoDimensionalSymbology.Qr, ErrorCorrectionLevel.L, "3100-0123|20261008-A|C2610080001|EC", "q");
        var high = MatrixSymbols.Encode(TwoDimensionalSymbology.Qr, ErrorCorrectionLevel.H, "3100-0123|20261008-A|C2610080001|EC", "q");
        check("ecLevel 对 QR 生效：H 比 L 模块多", high.Columns > low.Columns);
        var compact = MatrixSymbols.Encode(TwoDimensionalSymbology.Qr, ErrorCorrectionLevel.M, "3100-0123|20261008-A|C2610080001", "c");
        check("QR 用混合模式最小编码：示例箱标的内容在 M 级是版本 2（25×25），和设计器的 bwip-js 一样", compact.Columns == 25);
        var pdf = MatrixSymbols.Encode(TwoDimensionalSymbology.Pdf417, ErrorCorrectionLevel.M, "ROW-HEIGHT", "p");
        check("PDF417 每行高 3 个模块（和设计器一致）",
            pdf.Rows % 3 == 0 && Enumerable.Range(0, pdf.Rows / 3).All(row => Enumerable.Range(0, pdf.Columns).All(x => pdf[x, row * 3] == pdf[x, row * 3 + 1] && pdf[x, row * 3] == pdf[x, row * 3 + 2])));
        var square = MatrixSymbols.Encode(TwoDimensionalSymbology.DataMatrix, ErrorCorrectionLevel.M, "3100-0123|20261008-A|C2610080001", "d");
        check("DataMatrix 是正方形", square.Columns == square.Rows);

        CheckGeometry(renderer, check);
        CheckErrors(renderer, check);
    }

    /// <summary>
    /// 几何：模块大小按框和模块数取整、在框里居中、对齐打印点（含旋转 90°），quietZone 留白。
    /// </summary>
    /// <param name="renderer">渲染引擎。</param>
    /// <param name="check">记录检查结果。</param>
    private static void CheckGeometry(LabelRenderer renderer, Action<string, bool> check)
    {
        const string Value = "3100-0123|20261008-A|C2610080001";
        var symbol = MatrixSymbols.Encode(TwoDimensionalSymbology.Qr, ErrorCorrectionLevel.M, Value, "g");
        var element = new QrCodeElement { Id = "g", X = 10.37, Y = 10.11, Width = 30, Height = 30, Value = Value };
        using (var bitmap = Bitmap(renderer.Render(Job(Single(element), 300))))
        {
            var dots = (int)Math.Floor(LabelUnits.MmToDots(30, 300) / symbol.Columns);
            var bounds = Ink(bitmap);
            var center = LabelUnits.MmToDots(10.37 + 15, 300);
            check("模块大小 = floor(框边长点数 ÷ 模块数)，二维码在框里居中",
                bounds.Width == symbol.Columns * dots && Math.Abs(bounds.MidX - center) <= 1 && Math.Abs(bounds.MidY - LabelUnits.MmToDots(10.11 + 15, 300)) <= 1);
            var row = Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, (int)bounds.MidY).Red).ToArray();
            check("模块对齐打印点：没有灰色像素", row.All(value => value is 0 or 255));
        }

        var quiet = new QrCodeElement { Id = "g", X = 10, Y = 10, Width = 30, Height = 30, Value = Value, QuietZone = 5 };
        using (var bitmap = Bitmap(renderer.Render(Job(Single(quiet), 300))))
        {
            var bounds = Ink(bitmap);
            check("quietZone 生效：四周留出空白，二维码变小", bounds.Left >= LabelUnits.MmToDots(15, 300) - 1 && bounds.Right <= LabelUnits.MmToDots(35, 300) + 1);
        }

        var rotated = new QrCodeElement { Id = "r", X = 10.21, Y = 10.43, Width = 25, Height = 30, Value = Value, Rotation = 90 };
        using (var bitmap = Bitmap(renderer.Render(Job(Single(rotated), 300))))
        {
            var bounds = Ink(bitmap);
            var row = Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, (int)bounds.MidY).Red).ToArray();
            check("旋转 90° 的二维码能解码，模块仍对齐打印点", Decode(bitmap, BarcodeFormat.QR_CODE) == Value && row.All(value => value is 0 or 255));
        }
    }

    /// <summary>
    /// 错误码：模块小于 2 点时 BARCODE_TOO_DENSE，内容超出容量、内容为空时 BARCODE_INVALID。
    /// </summary>
    /// <param name="renderer">渲染引擎。</param>
    /// <param name="check">记录检查结果。</param>
    private static void CheckErrors(LabelRenderer renderer, Action<string, bool> check)
    {
        string? Error(QrCodeElement element)
        {
            try
            {
                renderer.Render(Job(Single(element), 203));
                return null;
            }
            catch (LabelRenderException ex)
            {
                return ex.ErrorCode;
            }
        }

        check("模块小于 2 点时返回 BARCODE_TOO_DENSE", Error(new QrCodeElement { Id = "e", X = 5, Y = 5, Width = 6, Height = 6, Value = new string('A', 120) }) == ErrorCodes.BarcodeTooDense);
        check("内容超出二维码容量返回 BARCODE_INVALID", Error(new QrCodeElement { Id = "e", X = 5, Y = 5, Width = 50, Height = 50, Value = new string('中', 3000) }) == ErrorCodes.BarcodeInvalid);
        check("内容为空返回 BARCODE_INVALID", Error(new QrCodeElement { Id = "e", X = 5, Y = 5, Width = 20, Height = 20, Value = "" }) == ErrorCodes.BarcodeInvalid);
    }

    /// <summary>
    /// 渲染一张 PNG 的任务。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="values">字段值；没有时为空。</param>
    /// <returns>渲染任务。</returns>
    private static RenderJob Job(LabelTemplate template, int dpi, IReadOnlyDictionary<string, object?>? values = null) =>
        new()
        {
            Template = template,
            Labels = [new LabelInstance(values ?? new Dictionary<string, object?>())],
            Format = LabelFormat.Png,
            Dpi = dpi
        };

    /// <summary>
    /// 只有一个元素的 90×60 模板。
    /// </summary>
    /// <param name="element">元素。</param>
    /// <returns>模板。</returns>
    private static LabelTemplate Single(LabelElement element) =>
        new()
        {
            Code = "MATRIX",
            Page = new LabelPage { Width = 90, Height = 60 },
            Elements = [element]
        };

    /// <summary>
    /// 把渲染结果解码成位图。
    /// </summary>
    /// <param name="result">PNG 渲染结果。</param>
    /// <returns>位图，用完释放。</returns>
    private static SKBitmap Bitmap(RenderResult result) => SKBitmap.Decode(result.Content.ToArray());

    /// <summary>
    /// 有墨迹的最小外接矩形（像素）。
    /// </summary>
    /// <param name="bitmap">位图。</param>
    /// <returns>外接矩形；没有墨迹时左上角大于右下角。</returns>
    private static SKRectI Ink(SKBitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = 0, bottom = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Red < 128)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x + 1);
                    bottom = Math.Max(bottom, y + 1);
                }
            }
        }

        return new SKRectI(left, top, right, bottom);
    }

    /// <summary>
    /// 用 ZXing.Net 解码位图里的码（只认指定的码制）。
    /// </summary>
    /// <param name="bitmap">位图。</param>
    /// <param name="format">码制。</param>
    /// <returns>解出的内容；解不出时为 null。</returns>
    private static string? Decode(SKBitmap bitmap, BarcodeFormat format)
    {
        var gray = new byte[bitmap.Width * bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                gray[y * bitmap.Width + x] = (byte)((pixel.Red + pixel.Green + pixel.Blue) / 3);
            }
        }

        var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { PossibleFormats = [format], TryHarder = true } };
        return reader.Decode(new RGBLuminanceSource(gray, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8))?.Text;
    }
}
