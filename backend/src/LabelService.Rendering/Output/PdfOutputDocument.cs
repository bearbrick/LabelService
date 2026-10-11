using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using SkiaSharp;

namespace LabelService.Rendering.Output;

/// <summary>
/// PDF：矢量输出，一张标签（一份）一页，页面尺寸等于模板的 mm 尺寸换算成 pt，字体由 Skia 嵌入子集。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>绘制器用打印点坐标画在 SKPictureRecorder 上；EndLabel 时把录好的画面按份数逐页回放，页面画布缩放 72/dpi。
/// 份数不需要重新排版，绘制器也不用知道自己在画 PDF。</item>
/// <item>条码的条宽是整数个打印点，换算成 pt 后仍是 72/dpi 的整数倍，按同样 DPI 的打印机打印时对齐。</item>
/// <item>Skia 按 RasterDpi（这里取输出 DPI）把页面尺寸对齐到打印点，和 PNG、ZPL 的像素尺寸一致，和 mm 尺寸的误差小于半个点。</item>
/// <item>文档写在内存里，最后一次性写给输出流。</item>
/// </list>
/// </remarks>
internal sealed class PdfOutputDocument : OutputDocument
{
    /// <summary>
    /// PDF 写在内存里，最后一次性写给输出流。
    /// </summary>
    private readonly MemoryStream buffer = new();

    /// <summary>
    /// Skia 的 PDF 文档。
    /// </summary>
    private readonly SKDocument document;

    /// <summary>
    /// 页面宽度（pt）。
    /// </summary>
    private readonly float pageWidth;

    /// <summary>
    /// 页面高度（pt）。
    /// </summary>
    private readonly float pageHeight;

    /// <summary>
    /// 正在录制的标签画面；没有正在绘制的标签时为 null。
    /// </summary>
    private SKPictureRecorder? recorder;

    /// <summary>
    /// 文档是否已经结束（结束后才能写出）。
    /// </summary>
    private bool closed;

    /// <summary>
    /// 创建 PDF 文档：页面尺寸按模板的 mm 尺寸换算成 pt。
    /// </summary>
    /// <param name="widthDots">标签宽度（打印点）。</param>
    /// <param name="heightDots">标签高度（打印点）。</param>
    /// <param name="dpi">分辨率（也用作 Skia 栅格化非矢量内容的 DPI）。</param>
    /// <param name="page">模板画布。</param>
    /// <exception cref="InvalidOperationException">Skia 无法创建 PDF。</exception>
    public PdfOutputDocument(int widthDots, int heightDots, int dpi, LabelPage page)
        : base(widthDots, heightDots, dpi)
    {
        pageWidth = (float)(page.Width / LabelUnits.MmPerInch * LabelUnits.PointsPerInch);
        pageHeight = (float)(page.Height / LabelUnits.MmPerInch * LabelUnits.PointsPerInch);
        var metadata = new SKDocumentPdfMetadata
        {
            Creator = "LabelService",
            Producer = "LabelService",
            RasterDpi = dpi,
        };
        document = SKDocument.CreatePdf(buffer, metadata) ?? throw new InvalidOperationException("无法创建 PDF 文档");
    }

    /// <inheritdoc />
    public override LabelFormat Format => LabelFormat.Pdf;

    /// <inheritdoc />
    public override SKCanvas BeginLabel()
    {
        if (recorder is not null)
        {
            throw new InvalidOperationException("上一张标签还没有结束。");
        }

        recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(WidthDots, HeightDots));
        canvas.Clear(SKColors.White);
        return canvas;
    }

    /// <inheritdoc />
    public override void EndLabel(int copies)
    {
        if (recorder is null)
        {
            throw new InvalidOperationException("没有正在绘制的标签。");
        }

        using var picture = recorder.EndRecording();
        recorder.Dispose();
        recorder = null;
        // 录好的画面按份数逐页回放，页面画布缩放 72/dpi，把打印点换成 pt。
        var scale = (float)(LabelUnits.PointsPerInch / Dpi);
        for (var copy = 0; copy < copies; copy++)
        {
            var page = document.BeginPage(pageWidth, pageHeight);
            page.Scale(scale);
            page.DrawPicture(picture);
            document.EndPage();
        }
    }

    /// <inheritdoc />
    public override void WriteTo(Stream output)
    {
        if (!closed)
        {
            document.Close();
            closed = true;
        }

        buffer.Position = 0;
        buffer.CopyTo(output);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        recorder?.Dispose();
        document.Dispose();
        buffer.Dispose();
    }
}
