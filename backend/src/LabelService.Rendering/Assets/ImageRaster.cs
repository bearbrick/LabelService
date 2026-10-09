using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Assets;

/// <summary>
/// 把图片按目标尺寸和 fit 栅格化，需要时抖动成黑白。纯计算，线程安全。
/// </summary>
internal static class ImageRaster
{
    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    /// <summary>
    /// 目标框里图片实际占的区域（相对框左上角）：contain 等比缩放后居中，cover 和 stretch 占满整个框。
    /// </summary>
    public static SKRect Destination(SKSize natural, float width, float height, ImageFit fit)
    {
        if (fit != ImageFit.Contain || natural.Width <= 0 || natural.Height <= 0)
        {
            return SKRect.Create(0, 0, width, height);
        }

        var scale = Math.Min(width / natural.Width, height / natural.Height);
        var w = natural.Width * scale;
        var h = natural.Height * scale;
        return SKRect.Create((width - w) / 2, (height - h) / 2, w, h);
    }

    /// <summary>
    /// 栅格化成 <paramref name="width"/>×<paramref name="height"/> 像素：contain 和 stretch 把整张图缩放到这个尺寸（contain 的宽高已按比例算好），
    /// cover 等比放大后居中裁掉多余部分。<paramref name="mono"/> 为 true 时抖动成黑白，白色部分透明。
    /// </summary>
    public static SKImage Render(ImageSource source, int width, int height, ImageFit fit, bool mono)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var natural = source.Size;
            var target = SKRect.Create(0, 0, width, height);
            var sourceRect = SKRect.Create(0, 0, natural.Width, natural.Height);
            if (fit == ImageFit.Cover && natural.Width > 0 && natural.Height > 0)
            {
                var scale = Math.Max(width / natural.Width, height / natural.Height);
                var cropWidth = width / scale;
                var cropHeight = height / scale;
                sourceRect = SKRect.Create((natural.Width - cropWidth) / 2, (natural.Height - cropHeight) / 2, cropWidth, cropHeight);
            }

            if (source.Raster is { } raster)
            {
                using var paint = new SKPaint { IsAntialias = true };
                canvas.DrawImage(raster, sourceRect, target, Sampling, paint);
            }
            else if (source.Vector is { } vector)
            {
                canvas.Save();
                canvas.ClipRect(target);
                canvas.Scale(width / sourceRect.Width, height / sourceRect.Height);
                canvas.Translate(-sourceRect.Left - vector.CullRect.Left, -sourceRect.Top - vector.CullRect.Top);
                canvas.DrawPicture(vector);
                canvas.Restore();
            }
        }

        if (mono)
        {
            Dither(bitmap);
        }

        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>
    /// Floyd–Steinberg 抖动：先按透明度叠在白底上算亮度，再逐点取黑白并把误差扩散给右、下方的点。黑点不透明，白点透明。
    /// </summary>
    /// <remarks>直接读写像素缓冲区（RGBA、预乘透明度），1000×1000 的图约几毫秒。</remarks>
    public static void Dither(SKBitmap bitmap)
    {
        if (bitmap.ColorType != SKColorType.Rgba8888 || bitmap.AlphaType != SKAlphaType.Premul)
        {
            throw new ArgumentException("只支持 RGBA8888 预乘透明度的位图", nameof(bitmap));
        }

        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = bitmap.GetPixelSpan();
        var luma = new float[width * height];
        for (var i = 0; i < luma.Length; i++)
        {
            // 预乘后的颜色已经乘过透明度，叠在白底上只需再加上 (255 - alpha)。
            var offset = i * 4;
            luma[i] = 0.299f * pixels[offset] + 0.587f * pixels[offset + 1] + 0.114f * pixels[offset + 2] + (255 - pixels[offset + 3]);
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var black = luma[index] < 128;
                var error = luma[index] - (black ? 0 : 255);
                var offset = index * 4;
                pixels[offset] = 0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = 0;
                pixels[offset + 3] = black ? (byte)255 : (byte)0;
                if (x + 1 < width)
                {
                    luma[index + 1] += error * 7 / 16;
                }

                if (y + 1 < height)
                {
                    if (x > 0)
                    {
                        luma[index + width - 1] += error * 3 / 16;
                    }

                    luma[index + width] += error * 5 / 16;
                    if (x + 1 < width)
                    {
                        luma[index + width + 1] += error * 1 / 16;
                    }
                }
            }
        }

        bitmap.NotifyPixelsChanged();
    }
}
