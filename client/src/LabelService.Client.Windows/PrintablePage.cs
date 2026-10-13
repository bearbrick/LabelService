using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace LabelService.Client.Printing;

/// <summary>
/// 一页图片及其物理尺寸，释放时同时关闭解码图像和输入流。
/// </summary>
internal sealed class PrintablePage : IDisposable
{
    /// <summary>
    /// 图像解码需要在整个图像生命周期保留的流。
    /// </summary>
    private readonly MemoryStream stream;

    /// <summary>
    /// 从内存创建一页；PDF 传入准确的物理尺寸，PNG 由 DPI 换算。
    /// </summary>
    /// <param name="content">PNG 内容。</param>
    /// <param name="widthInches">页面宽度，英寸。</param>
    /// <param name="heightInches">页面高度，英寸。</param>
    public PrintablePage(byte[] content, float widthInches, float heightInches)
    {
        stream = new MemoryStream(content, writable: false);
        try
        {
            Image = Image.FromStream(stream);
            WidthInches = widthInches;
            HeightInches = heightInches;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 创建 PNG 页面，并按服务响应的 DPI 换算实际尺寸。
    /// </summary>
    /// <param name="content">PNG 内容。</param>
    /// <param name="dpi">生成 DPI。</param>
    /// <returns>页面。</returns>
    public static PrintablePage FromPng(byte[] content, int dpi)
    {
        var page = new PrintablePage(content, 0, 0);
        page.WidthInches = page.Image.Width / (float)dpi;
        page.HeightInches = page.Image.Height / (float)dpi;
        return page;
    }

    /// <summary>
    /// 已解码图像。
    /// </summary>
    public Image Image
    {
        get; private set;
    }

    /// <summary>
    /// 物理宽度，英寸。
    /// </summary>
    public float WidthInches
    {
        get; private set;
    }

    /// <summary>
    /// 物理高度，英寸。
    /// </summary>
    public float HeightInches
    {
        get; private set;
    }

    /// <summary>
    /// 将系统 PDF 引擎受桌面缩放影响的位图校正到请求 DPI 对应的像素尺寸，物理尺寸保持不变。
    /// </summary>
    /// <param name="width">目标像素宽度。</param>
    /// <param name="height">目标像素高度。</param>
    internal void NormalizePixels(int width, int height)
    {
        if (Image.Width == width && Image.Height == height)
        {
            return;
        }
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(Image, new Rectangle(0, 0, width, height),
                    new Rectangle(0, 0, Image.Width, Image.Height), GraphicsUnit.Pixel);
            }
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
        Image.Dispose();
        Image = bitmap;
    }

    /// <summary>
    /// 关闭页面图像和保留的输入流。
    /// </summary>
    public void Dispose()
    {
        Image.Dispose();
        stream.Dispose();
    }
}
