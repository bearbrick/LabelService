using System.Buffers.Binary;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Rendering.Output;
using LabelService.Rendering.Validation;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// PNG 编码（T-021，<see cref="PngWriter"/>）：无损（灰度、彩色逐像素一致，黑白按阈值），格式按内容选（灰度 / RGB / 1 位），能被独立的解码器读出。
/// </summary>
internal static class PngChecks
{
    public static void Run(string backendRoot, Action<string, bool> check)
    {
        using var gray = Canvas((canvas, width, height) =>
        {
            using var paint = new SKPaint { IsAntialias = true, Color = SKColors.Black };
            canvas.DrawCircle(width / 3f, height / 2f, height / 3f, paint);
            paint.Color = new SKColor(0x80, 0x80, 0x80);
            canvas.DrawRect(SKRect.Create(width / 2f, 10, width / 3f, height - 20), paint);
            using var font = new SKFont(SKTypeface.Default, 40);
            canvas.DrawText("Abc 123", 20, height - 20, SKTextAlign.Left, font, paint);
        });
        var grayPng = PngWriter.Encode(gray, mono: false);
        check("没有彩色的画面编码成 8 位灰度 PNG，解码后和原图逐像素一致", Header(grayPng) == (8, 0) && SamePixels(gray, grayPng));

        using var color = Canvas((canvas, width, height) =>
        {
            using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xD4, 0x3A, 0x3A) };
            canvas.DrawOval(SKRect.Create(10, 10, width - 20, height - 20), paint);
            paint.Color = new SKColor(0x15, 0x55, 0xD4);
            canvas.DrawRect(SKRect.Create(width / 4f, height / 4f, width / 2f, height / 2f), paint);
        });
        var colorPng = PngWriter.Encode(color, mono: false);
        check("有彩色的画面编码成 8 位 RGB（不带 alpha），解码后逐像素一致", Header(colorPng) == (8, 2) && SamePixels(color, colorPng));

        var monoPng = PngWriter.Encode(gray, mono: true);
        using var monoDecoded = SKBitmap.Decode(monoPng);
        var monoOk = Header(monoPng) == (1, 0);
        for (var y = 0; y < gray.Height && monoOk; y++)
        {
            for (var x = 0; x < gray.Width && monoOk; x++)
            {
                var source = gray.GetPixel(x, y);
                var expected = Monochrome.IsBlack(source.Red, source.Green, source.Blue) ? (byte)0 : (byte)255;
                var actual = monoDecoded.GetPixel(x, y);
                monoOk = actual.Red == expected && actual.Green == expected && actual.Blue == expected;
            }
        }

        check("mono 编码成 1 位灰度，每个像素按和 ZPL 相同的阈值变成黑或白", monoOk);

        var renderer = Fixtures.Renderer(backendRoot);
        var template = Fixtures.LoadTemplate(backendRoot);
        var values = FieldValidator.Validate(template.Fields, null, Fixtures.Data(Fixtures.BoxLabelData)).Values;
        var box = renderer.Render(new RenderJob { Template = template, Labels = [new LabelInstance(values)], Format = LabelFormat.Png, Dpi = 300 }).Content.ToArray();
        using var boxDecoded = SKBitmap.Decode(box);
        check("示例箱标（黑白灰）输出 8 位灰度 PNG，1181×709，能被解码", Header(box) == (8, 0) && boxDecoded is { Width: 1181, Height: 709 });
    }

    private static SKBitmap Canvas(Action<SKCanvas, int, int> draw)
    {
        var bitmap = new SKBitmap(new SKImageInfo(331, 197, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        draw(canvas, bitmap.Width, bitmap.Height);
        return bitmap;
    }

    /// <summary>
    /// IHDR 里的位深和颜色类型（0 灰度、2 RGB）。
    /// </summary>
    private static (int BitDepth, int ColorType) Header(byte[] png) =>
        BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(12)) == 0x49484452 ? (png[24], png[25]) : (-1, -1);

    private static bool SamePixels(SKBitmap original, byte[] png)
    {
        using var decoded = SKBitmap.Decode(png);
        if (decoded is null || decoded.Width != original.Width || decoded.Height != original.Height)
        {
            return false;
        }

        for (var y = 0; y < original.Height; y++)
        {
            for (var x = 0; x < original.Width; x++)
            {
                var a = original.GetPixel(x, y);
                var b = decoded.GetPixel(x, y);
                if (a.Red != b.Red || a.Green != b.Green || a.Blue != b.Blue)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
