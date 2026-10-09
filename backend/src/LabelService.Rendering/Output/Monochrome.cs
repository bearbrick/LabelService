using SkiaSharp;

namespace LabelService.Rendering.Output;

/// <summary>
/// 黑白化：PNG 的 mono 和 ZPL 共用。
/// </summary>
/// <remarks>
/// 黑白输出时绘制器已经按黑白画（文字、线条不抗锯齿，图片用 Floyd–Steinberg 抖动），这里只按亮度阈值把剩下的颜色归成黑或白：
/// 亮度低于 128 为黑。浅色底纹（如 #EEEEEE）会变白，深色（如红色文字）会变黑。
/// </remarks>
internal static class Monochrome
{
    /// <summary>
    /// 亮度阈值，低于它为黑。
    /// </summary>
    public const int Threshold = 128;

    /// <summary>
    /// 把 RGBA 位图按阈值打包成 1 位：每行 <c>(宽 + 7) / 8</c> 字节，高位在左，1 为黑（ZPL ^GF 的格式）。
    /// </summary>
    public static byte[] Pack(SKBitmap bitmap, out int bytesPerRow)
    {
        if (bitmap.ColorType != SKColorType.Rgba8888)
        {
            throw new ArgumentException("只支持 RGBA8888 位图", nameof(bitmap));
        }

        var width = bitmap.Width;
        var height = bitmap.Height;
        bytesPerRow = (width + 7) / 8;
        var packed = new byte[bytesPerRow * height];
        var pixels = bitmap.GetPixelSpan();
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            var target = y * bytesPerRow;
            for (var x = 0; x < width; x++)
            {
                var offset = row + x * 4;
                if (IsBlack(pixels[offset], pixels[offset + 1], pixels[offset + 2]))
                {
                    packed[target + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }

        return packed;
    }

    /// <summary>
    /// 把 RGBA 位图原地转成纯黑白（PNG 的 mono）。
    /// </summary>
    public static void Apply(SKBitmap bitmap)
    {
        var pixels = bitmap.GetPixelSpan();
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var value = IsBlack(pixels[offset], pixels[offset + 1], pixels[offset + 2]) ? (byte)0 : (byte)255;
            pixels[offset] = value;
            pixels[offset + 1] = value;
            pixels[offset + 2] = value;
            pixels[offset + 3] = 255;
        }

        bitmap.NotifyPixelsChanged();
    }

    /// <summary>
    /// 按 ITU-R BT.601 亮度判断黑白（白底不透明位图）。
    /// </summary>
    private static bool IsBlack(byte red, byte green, byte blue) => (299 * red + 587 * green + 114 * blue) / 1000 < Threshold;
}
