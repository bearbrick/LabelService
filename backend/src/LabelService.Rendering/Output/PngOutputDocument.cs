using LabelService.Contracts.Protocol;
using SkiaSharp;

namespace LabelService.Rendering.Output;

/// <summary>
/// PNG：一份文档只含一张标签、一份。批量 PNG 由服务端逐张渲染后打包 ZIP。
/// </summary>
/// <remarks>
/// 用最低的 zlib 压缩等级、不做行过滤，以体积换速度（单张预算见 docs/performance.md）。
/// mono 黑白化尚未实现（T-006 和 ZPL 共用同一个黑白化函数）。
/// </remarks>
internal sealed class PngOutputDocument(int widthDots, int heightDots, int dpi, bool mono)
    : OutputDocument(widthDots, heightDots, dpi)
{
    private SKBitmap? bitmap;
    private SKCanvas? canvas;
    private SKData? encoded;

    public override LabelFormat Format => LabelFormat.Png;

    /// <summary>
    /// 是否要求黑白输出。
    /// </summary>
    public bool Mono { get; } = mono;

    public override SKCanvas BeginLabel()
    {
        if (bitmap is not null)
        {
            throw new InvalidOperationException("PNG 文档只能包含一张标签。");
        }

        bitmap = new SKBitmap(new SKImageInfo(WidthDots, HeightDots, SKColorType.Rgba8888, SKAlphaType.Opaque));
        canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        return canvas;
    }

    public override void EndLabel(int copies)
    {
        if (bitmap is null || canvas is null)
        {
            throw new InvalidOperationException("没有正在绘制的标签。");
        }

        if (copies != 1)
        {
            throw new InvalidOperationException("PNG 只能输出 1 份，份数校验应在接口层完成。");
        }

        canvas.Flush();
        using var pixmap = bitmap.PeekPixels();
        encoded = pixmap.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.NoFilters, 1));
    }

    public override void WriteTo(Stream output)
    {
        if (encoded is null)
        {
            throw new InvalidOperationException("还没有完成任何标签。");
        }

        encoded.SaveTo(output);
    }

    public override void Dispose()
    {
        encoded?.Dispose();
        canvas?.Dispose();
        bitmap?.Dispose();
    }
}
