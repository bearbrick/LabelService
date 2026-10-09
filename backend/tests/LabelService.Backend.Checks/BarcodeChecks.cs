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
/// 一维码（T-002）：各码制编码和校验位、模块对齐打印点、过密和非法内容的错误码、人眼可读文字、对齐、旋转，
/// 以及用 ZXing.Net 把渲染出的 PNG 解码回来核对内容。
/// </summary>
internal static class BarcodeChecks
{
    public static void Run(string backendRoot, Action<string, bool> check)
    {
        var renderer = new LabelRenderer(Fixtures.Fonts(backendRoot));
        CheckSample(backendRoot, renderer, check);
        CheckSymbologies(renderer, check);
        CheckDots(renderer, check);
        CheckErrors(renderer, check);
        CheckLayout(renderer, check);
    }

    private static void CheckSample(string backendRoot, LabelRenderer renderer, Action<string, bool> check)
    {
        var template = Fixtures.LoadTemplate(backendRoot);
        var values = FieldValidator.Validate(template.Fields, null, Fixtures.Data(Fixtures.BoxLabelData)).Values;
        foreach (var dpi in new[] { 203, 300 })
        {
            using var bitmap = Bitmap(renderer.Render(Job(template, dpi, values)));
            check($"示例箱标的条码 {dpi}dpi 能解码，内容是箱号", Decode(bitmap, BarcodeFormat.CODE_128)?.Text == "C2610080001");
        }
    }

    private static void CheckSymbologies(LabelRenderer renderer, Action<string, bool> check)
    {
        (BarcodeSymbology Symbology, string Value, BarcodeFormat Format, string Expected)[] cases =
        [
            (BarcodeSymbology.Code128, "Box-2026/10#01", BarcodeFormat.CODE_128, "Box-2026/10#01"),
            (BarcodeSymbology.Code39, "LOT-42 A", BarcodeFormat.CODE_39, "LOT-42 A"),
            (BarcodeSymbology.Ean13, "400638133393", BarcodeFormat.EAN_13, "4006381333931"),
            (BarcodeSymbology.Ean13, "4006381333931", BarcodeFormat.EAN_13, "4006381333931"),
            (BarcodeSymbology.Ean8, "9638507", BarcodeFormat.EAN_8, "96385074"),
            (BarcodeSymbology.Upca, "03600029145", BarcodeFormat.UPC_A, "036000291452"),
            (BarcodeSymbology.Itf14, "1540014128876", BarcodeFormat.ITF, "15400141288763"),
            (BarcodeSymbology.Gs1128, "(01)04006381333931(10)AB12(17)261231", BarcodeFormat.CODE_128, "]C1010400638133393110AB12\u001d17261231"),
        ];
        foreach (var dpi in new[] { 203, 300 })
        {
            var failed = new List<string>();
            foreach (var (symbology, value, format, expected) in cases)
            {
                // 最长的 GS1-128 例子有 255 个模块，203dpi 下至少要 64 mm 宽。
                using var bitmap = Bitmap(renderer.Render(Job(Single(new BarcodeElement { Id = "b", X = 10, Y = 5, Width = 80, Height = 15, Symbology = symbology, Value = value }, width: 100), dpi)));
                var decoded = Decode(bitmap, format, gs1: symbology == BarcodeSymbology.Gs1128);
                if (decoded?.Text != expected)
                {
                    failed.Add($"{LinearSymbols.Name(symbology)} {value} → {decoded?.Text ?? "解不出"}");
                }
            }

            check($"7 种码制 {dpi}dpi 都能解码回原内容（EAN、UPC、ITF-14 自动补校验位）" + (failed.Count > 0 ? "：" + string.Join("；", failed) : ""), failed.Count == 0);
        }

        check("校验位算法（GS1）", LinearSymbols.CheckDigit("400638133393") == 1 && LinearSymbols.CheckDigit("03600029145") == 2);
        using var gs1 = Bitmap(renderer.Render(Job(Single(new BarcodeElement { Id = "g", X = 10, Y = 5, Width = 60, Height = 15, Symbology = BarcodeSymbology.Gs1128, Value = "(01)04006381333931" }), 300)));
        object? symbologyId = null;
        Decode(gs1, BarcodeFormat.CODE_128)?.ResultMetadata?.TryGetValue(ResultMetadataType.SYMBOLOGY_IDENTIFIER, out symbologyId);
        check("GS1-128 开头带 FNC1（扫描枪识别为 ]C1）", symbologyId as string == "]C1");
    }

    private static void CheckDots(LabelRenderer renderer, Action<string, bool> check)
    {
        foreach (var dpi in new[] { 203, 300 })
        {
            var element = new BarcodeElement { Id = "b", X = 10.3, Y = 5.2, Width = 60, Height = 15, Value = "C2610080001", ShowText = false };
            var result = renderer.Render(Job(Single(element), dpi));
            using var bitmap = Bitmap(result);
            var symbol = LinearSymbols.Encode(BarcodeSymbology.Code128, "C2610080001", "b");
            var dots = (int)Math.Floor(LabelUnits.MmToDots(60, dpi) / symbol.Columns);
            var runs = Runs(Row(bitmap, (int)LabelUnits.MmToDots(12, dpi)));
            check($"{dpi}dpi：每条和每个空都是整数个模块宽（每模块 {dots} 点），没有灰色像素",
                runs.Pure && runs.Lengths.Skip(1).SkipLast(1).All(length => length % dots == 0) && runs.Lengths.Skip(1).SkipLast(1).Sum() == symbol.Columns * dots);
        }

        var rotated = new BarcodeElement { Id = "r", X = 20.15, Y = 25.07, Width = 50, Height = 12, Value = "ROTATE-90", Rotation = 90, ShowText = false };
        using var rotatedBitmap = Bitmap(renderer.Render(Job(Single(rotated, 90, 80), 300)));
        var column = Runs(Column(rotatedBitmap, (int)LabelUnits.MmToDots(45, 300)));
        var rotatedDots = (int)Math.Floor(LabelUnits.MmToDots(50, 300) / LinearSymbols.Encode(BarcodeSymbology.Code128, "ROTATE-90", "r").Columns);
        check("旋转 90° 后条边仍落在整点上（逐行扫描没有灰色像素，条宽是模块的整数倍）",
            column.Pure && column.Lengths.Skip(1).SkipLast(1).All(length => length % rotatedDots == 0));
        check("旋转 90° 的条码能解码", Decode(rotatedBitmap, BarcodeFormat.CODE_128, autoRotate: true)?.Text == "ROTATE-90");
        var tilted = new BarcodeElement { Id = "t", X = 20, Y = 25, Width = 50, Height = 12, Value = "TILT", Rotation = 47 };
        var layout = new BarcodePainter().Layout(tilted, Context(300));
        check("非直角的旋转按最近的直角画", layout.Rotation == 90);
    }

    private static void CheckErrors(LabelRenderer renderer, Action<string, bool> check)
    {
        string? Error(BarcodeElement element)
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

        BarcodeElement Bar(BarcodeSymbology symbology, string value, double width = 60) =>
            new()
            {
                Id = "e",
                X = 5,
                Y = 5,
                Width = width,
                Height = 15,
                Symbology = symbology,
                Value = value
            };

        check("模块宽度小于 minModuleDots 时返回 BARCODE_TOO_DENSE", Error(Bar(BarcodeSymbology.Code128, "C2610080001", width: 20)) == ErrorCodes.BarcodeTooDense);
        check("minModuleDots 可以调低", Error(new BarcodeElement { Id = "e", X = 5, Y = 5, Width = 20, Height = 15, Value = "C2610080001", MinModuleDots = 1 }) is null);
        check("EAN-13 不是数字返回 BARCODE_INVALID", Error(Bar(BarcodeSymbology.Ean13, "40063813339A")) == ErrorCodes.BarcodeInvalid);
        check("EAN-13 位数不对返回 BARCODE_INVALID", Error(Bar(BarcodeSymbology.Ean13, "40063813")) == ErrorCodes.BarcodeInvalid);
        check("EAN-13 校验位错返回 BARCODE_INVALID", Error(Bar(BarcodeSymbology.Ean13, "4006381333932")) == ErrorCodes.BarcodeInvalid);
        check("Code128 含中文返回 BARCODE_INVALID", Error(Bar(BarcodeSymbology.Code128, "箱号123")) == ErrorCodes.BarcodeInvalid);
        check("GS1-128 不是 (AI)数据 的写法返回 BARCODE_INVALID", Error(Bar(BarcodeSymbology.Gs1128, "0104006381333931")) == ErrorCodes.BarcodeInvalid);
        check("GS1-128 定长 AI 位数不对返回 BARCODE_INVALID", Error(Bar(BarcodeSymbology.Gs1128, "(01)123")) == ErrorCodes.BarcodeInvalid);
        check("内容为空返回 BARCODE_INVALID", Error(Bar(BarcodeSymbology.Code128, "")) == ErrorCodes.BarcodeInvalid);
    }

    private static void CheckLayout(LabelRenderer renderer, Action<string, bool> check)
    {
        int Dark(BarcodeElement element, double x, double y, double width, double height)
        {
            using var bitmap = Bitmap(renderer.Render(Job(Single(element), 300)));
            return DarkPixels(bitmap, x, y, width, height);
        }

        BarcodeElement Bar() => new()
        {
            Id = "c",
            X = 10,
            Y = 5,
            Width = 60,
            Height = 15,
            Value = "C2610080001"
        };
        check("人眼可读文字画在条码下方", Dark(Bar(), 10, 17, 60, 3) > 50);
        var hidden = Bar();
        hidden.ShowText = false;
        check("showText=false 时条一直画到框底，下面没有文字", Dark(hidden, 10, 19.5, 60, 0.4) > 50 && Dark(hidden, 10, 20.2, 60, 3) == 0);
        var top = Bar();
        top.TextPosition = BarcodeTextPosition.Top;
        check("textPosition=top 时文字在上、条在下", Dark(top, 10, 5, 60, 3) > 50 && Dark(top, 10, 19.5, 60, 0.4) > 50);
        var big = Bar();
        big.TextStyle = new TextStyle { FontSize = 12 };
        check("textStyle 字号生效：字号变大，文字占的高度变大，条变矮", Dark(big, 10, 15.0, 60, 0.3) < Dark(Bar(), 10, 15.0, 60, 0.3) / 2 && Dark(big, 10, 15.5, 60, 4.5) > 50);

        var symbol = LinearSymbols.Encode(BarcodeSymbology.Code128, "C2610080001", "c");
        var width = symbol.Columns * Math.Floor(LabelUnits.MmToDots(60, 300) / symbol.Columns) / 300 * LabelUnits.MmPerInch;
        var right = Bar();
        right.Align = TextAlign.Right;
        right.ShowText = false;
        var center = Bar();
        center.Align = TextAlign.Center;
        center.ShowText = false;
        check("align：右对齐时条码贴框右边、左边留白；居中时两边留白", Dark(right, 10, 6, 60 - width - 0.3, 12) == 0 && Dark(right, 69.5, 6, 0.5, 12) > 0
            && Dark(center, 10, 6, (60 - width) / 2 - 0.2, 12) == 0 && Dark(center, 70 - (60 - width) / 2 + 0.2, 6, (60 - width) / 2 - 0.3, 12) == 0);
        var quiet = Bar();
        quiet.QuietZone = 5;
        quiet.MinModuleDots = 1;
        check("quietZone 两侧留白生效", Dark(quiet, 10, 6, 4.8, 12) == 0 && Dark(quiet, 15, 6, 1, 12) > 0);

        var first = LinearSymbols.Encode(BarcodeSymbology.Code128, "CACHE-1", "x");
        check("同样的码制和内容只编码一次（缓存）", ReferenceEquals(first, LinearSymbols.Encode(BarcodeSymbology.Code128, "CACHE-1", "y")) && SymbolCache.Count <= SymbolCache.Capacity);
    }

    private static RenderJob Job(LabelTemplate template, int dpi, IReadOnlyDictionary<string, object?>? values = null) =>
        new()
        {
            Template = template,
            Labels = [new LabelInstance(values ?? new Dictionary<string, object?>())],
            Format = LabelFormat.Png,
            Dpi = dpi
        };

    private static LabelTemplate Single(LabelElement element, double width = 80, double height = 30) =>
        new()
        {
            Code = "BARCODE",
            Page = new LabelPage { Width = width, Height = height },
            Elements = [element]
        };

    private static RenderContext Context(int dpi) =>
        new(dpi, false, new Dictionary<string, object?>(), new Dictionary<string, FieldDefinition>(), Rendering.Fonts.FontRegistry.Empty, new WarningList());

    private static SKBitmap Bitmap(RenderResult result) => SKBitmap.Decode(result.Content.ToArray());

    /// <summary>
    /// 解码。<paramref name="gs1"/> 为 true 时按 GS1 解读：开头的 FNC1 输出为 ]C1，中间的 FNC1 输出为 GS（0x1D）。
    /// </summary>
    private static Result? Decode(SKBitmap bitmap, BarcodeFormat format, bool autoRotate = false, bool gs1 = false)
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

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = autoRotate,
            Options = new DecodingOptions { PossibleFormats = [format], TryHarder = true, UseCode39ExtendedMode = true, AssumeGS1 = gs1 },
        };
        return reader.Decode(new RGBLuminanceSource(gray, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8));
    }

    private static byte[] Row(SKBitmap bitmap, int y) => Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, y).Red).ToArray();

    private static byte[] Column(SKBitmap bitmap, int x) => Enumerable.Range(0, bitmap.Height).Select(y => bitmap.GetPixel(x, y).Red).ToArray();

    /// <summary>
    /// 一行像素的黑白游程长度。Pure 表示只有纯黑和纯白。第一段和最后一段是两侧空白。
    /// </summary>
    private static (bool Pure, List<int> Lengths) Runs(byte[] pixels)
    {
        var lengths = new List<int>();
        var pure = pixels.All(value => value is 0 or 255);
        var run = 1;
        for (var i = 1; i < pixels.Length; i++)
        {
            if ((pixels[i] < 128) == (pixels[i - 1] < 128))
            {
                run++;
            }
            else
            {
                lengths.Add(run);
                run = 1;
            }
        }

        lengths.Add(run);
        return (pure, lengths);
    }

    private static int DarkPixels(SKBitmap bitmap, double xMm, double yMm, double widthMm, double heightMm)
    {
        int Dot(double mm) => (int)Math.Round(LabelUnits.MmToDots(mm, 300));
        var count = 0;
        for (var y = Math.Max(0, Dot(yMm)); y < Math.Min(bitmap.Height, Dot(yMm + heightMm)); y++)
        {
            for (var x = Math.Max(0, Dot(xMm)); x < Math.Min(bitmap.Width, Dot(xMm + widthMm)); x++)
            {
                if (bitmap.GetPixel(x, y).Red < 128)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
