using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;

namespace LabelService.Client.Printing;

/// <summary>
/// Windows 桌面静默打印入口：PNG 和 PDF 通过驱动打印，ZPL 原样发送给支持 ZPL 的打印机。
/// </summary>
/// <remarks>
/// 默认沿用实施人员配置的纸张、方向、纸盒和份数；内容保持实际尺寸，不自动缩放。
/// PDF 使用 Windows 自带的渲染引擎，需要 Windows 8.1 或更新版本；不需要 PDF 阅读器。
/// 方法返回表示任务提交结束，不代表打印机已经出纸。
/// </remarks>
public static class WindowsLabelPrinter
{
    /// <summary>
    /// 直接静默打印生成结果；不弹打印机选择框和 .NET 打印进度框。
    /// </summary>
    /// <param name="file">PNG、PDF、ZPL 或相应格式的 ZIP。</param>
    /// <param name="printerName">Windows 打印机名称；不传或空字符串时使用系统默认打印机。</param>
    public static void Print(LabelFile file, string? printerName = null)
    {
        Validate(file);
        if (file.Format == LabelFormat.Zpl)
        {
            var settings = new PrinterSettings();
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                settings.PrinterName = printerName;
            }

            var name = settings.PrinterName;
            if (name == null || name.Trim().Length == 0 || !settings.IsValid)
            {
                throw new InvalidPrinterException(settings);
            }

            // RAW 不经过驱动图形排版；份数和 DPI 由生成的 ZPL 指令决定。
            var count = 0;
            foreach (var content in PrintPages.Files(file))
            {
                WindowsRawPrinter.Send(name, content, file.FileName);
                count++;
            }

            if (count == 0)
            {
                throw new InvalidDataException("压缩包中没有可打印的 ZPL 文件。");
            }

            return;
        }

        using (var document = CreateDocument(file, printerName))
        {
            document.Print();
        }
    }

    /// <summary>
    /// 创建 PNG 或 PDF 的打印文档，特殊情况可先修改标准参数，再调用 Print；使用完必须释放。
    /// </summary>
    /// <param name="file">PNG、PDF 或相应格式的 ZIP。</param>
    /// <param name="printerName">Windows 打印机名称；不传时使用系统默认打印机。</param>
    /// <returns>准备好分页内容和静默控制器的打印文档。</returns>
    public static PrintDocument CreateDocument(LabelFile file, string? printerName = null)
    {
        Validate(file);
        if (file.Format == LabelFormat.Zpl)
        {
            throw new ArgumentException("ZPL 使用原始指令，请调用 Print；份数等参数在生成请求中设置。", nameof(file));
        }

        var document = new PrintDocument
        {
            DocumentName = file.FileName,
            PrintController = new StandardPrintController(),
        };
        if (!string.IsNullOrWhiteSpace(printerName))
        {
            document.PrinterSettings.PrinterName = printerName;
        }

        IEnumerator<PrintablePage>? pages = null;
        document.BeginPrint += (_, _) =>
        {
            pages?.Dispose();
            pages = PrintPages.Read(file).GetEnumerator();
            try
            {
                if (!pages.MoveNext())
                {
                    throw new InvalidDataException("生成结果中没有可打印页面。");
                }
            }
            catch
            {
                pages.Dispose();
                pages = null;
                throw;
            }
        };
        document.PrintPage += (_, e) =>
        {
            if (pages == null || e.Graphics == null)
            {
                throw new InvalidOperationException("打印页面尚未准备好。");
            }

            // 默认按纸张原点还原模板位置；启用 OriginAtMargins 时尊重调用方设定的边距。
            var left = document.OriginAtMargins ? 0f : -e.PageSettings.HardMarginX / 100f;
            var top = document.OriginAtMargins ? 0f : -e.PageSettings.HardMarginY / 100f;
            DrawPage(e.Graphics, pages.Current, left, top);
            e.HasMorePages = pages.MoveNext();
        };
        document.EndPrint += (_, _) =>
        {
            pages?.Dispose();
            pages = null;
        };
        document.Disposed += (_, _) => pages?.Dispose();
        return document;
    }

    /// <summary>
    /// 按页面的准确物理尺寸绘制，并恢复外部绘图状态。
    /// </summary>
    /// <param name="graphics">页面绘图上下文。</param>
    /// <param name="page">当前页。</param>
    /// <param name="leftInches">相对绘图原点的水平位置，英寸。</param>
    /// <param name="topInches">相对绘图原点的垂直位置，英寸。</param>
    internal static void DrawPage(Graphics graphics, PrintablePage page, float leftInches, float topInches)
    {
        var state = graphics.Save();
        try
        {
            graphics.PageUnit = GraphicsUnit.Inch;
            graphics.DrawImage(page.Image,
                new RectangleF(leftInches, topInches, page.WidthInches, page.HeightInches),
                new RectangleF(0, 0, page.Image.Width, page.Image.Height), GraphicsUnit.Pixel);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>
    /// 核对生成结果和当前平台，在打开打印任务之前拒绝无效输入。
    /// </summary>
    /// <param name="file">生成结果。</param>
    private static void Validate(LabelFile file)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            throw new PlatformNotSupportedException("WindowsLabelPrinter 只能用于 Windows 桌面客户端。");
        }

        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (file.Format != LabelFormat.Png && file.Format != LabelFormat.Pdf && file.Format != LabelFormat.Zpl)
        {
            throw new ArgumentException("打印格式只能是 PNG、PDF 或 ZPL。", nameof(file));
        }

        if (file.Content.Length == 0 || file.Dpi <= 0)
        {
            throw new ArgumentException("生成结果缺少内容或有效的 DPI。", nameof(file));
        }
    }
}
