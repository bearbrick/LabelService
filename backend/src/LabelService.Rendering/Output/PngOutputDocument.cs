using LabelService.Contracts.Protocol;
using SkiaSharp;

namespace LabelService.Rendering.Output;

/// <summary>
/// PNG：一份文档只含一张标签、一份。批量 PNG 由服务端逐张渲染后打包 ZIP。
/// </summary>
/// <remarks>
/// 编码见 <see cref="PngWriter"/>：没有彩色时输出 8 位灰度，有彩色时输出 RGB；mono 为 true 时输出 1 位黑白（和 ZPL 同一个阈值）。
/// </remarks>
internal sealed class PngOutputDocument(int widthDots, int heightDots, int dpi, bool mono)
    : OutputDocument(widthDots, heightDots, dpi)
{
    private SKBitmap? bitmap;
    private SKCanvas? canvas;
    private byte[]? encoded;

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
        encoded = PngWriter.Encode(bitmap, Mono);
    }

    public override void WriteTo(Stream output)
    {
        if (encoded is null)
        {
            throw new InvalidOperationException("还没有完成任何标签。");
        }

        output.Write(encoded);
    }

    public override void Dispose()
    {
        canvas?.Dispose();
        bitmap?.Dispose();
    }
}
