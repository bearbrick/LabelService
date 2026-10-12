#if WINDOWS
using System.Drawing;
using System.Drawing.Printing;
using System.Reflection;
using System.Runtime.InteropServices;
using LabelService.Client.Printing;
#endif

namespace LabelService.Client.Checks;

/// <summary>
/// Windows 静默打印检查，在内存画布验证真实 PDF、PNG、ZIP 和分页，不调用 Print。
/// </summary>
internal static class WindowsPrintChecks
{
    /// <summary>
    /// 运行 Windows 检查；其他系统明确跳过桌面打印。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    public static void Run(Action<string, bool> check)
    {
#if WINDOWS
        var png = PrintFixtures.File(PrintFixtures.Png(Color.Red), LabelFormat.Png);
        using (var document = WindowsLabelPrinter.CreateDocument(png))
        using (var defaults = new PrintDocument())
        {
            check("Windows 打印使用默认打印机和静默控制器", document.PrintController is StandardPrintController
                && document.PrinterSettings.PrinterName == defaults.PrinterSettings.PrinterName);
            check("Windows 打印不覆盖驱动份数和定位模式", document.PrinterSettings.Copies == defaults.PrinterSettings.Copies
                && document.OriginAtMargins == defaults.OriginAtMargins);
            if (defaults.PrinterSettings.IsValid)
            {
                check("Windows 打印不覆盖纸张和方向", document.DefaultPageSettings.PaperSize.Width == defaults.DefaultPageSettings.PaperSize.Width
                    && document.DefaultPageSettings.PaperSize.Height == defaults.DefaultPageSettings.PaperSize.Height
                    && document.DefaultPageSettings.Landscape == defaults.DefaultPageSettings.Landscape);
            }
        }

        using (var document = WindowsLabelPrinter.CreateDocument(png, "打印检查的合成名称"))
        {
            document.OriginAtMargins = true;
            document.PrinterSettings.Copies = 2;
            Raise(document, "OnBeginPrint", new PrintEventArgs());
            check("Windows 单张 PNG 保持实际尺寸并结束分页", Page(document, Color.Red, 100));
            Raise(document, "OnEndPrint", new PrintEventArgs());
            check("Windows 特殊参数可在打印前修改", document.PrinterSettings.Copies == 2
                && document.PrinterSettings.PrinterName == "打印检查的合成名称");
        }

        var zipped = PrintFixtures.File(PrintFixtures.Zip(("z.png", PrintFixtures.Png(Color.Red)),
            ("a.png", PrintFixtures.Png(Color.Blue))), LabelFormat.Png, true);
        using (var document = WindowsLabelPrinter.CreateDocument(zipped))
        {
            document.OriginAtMargins = true;
            Raise(document, "OnBeginPrint", new PrintEventArgs());
            check("PNG ZIP 按服务条目顺序打印第一页", !Page(document, Color.Red, 100));
            check("PNG ZIP 同一个文档连续打印到最后一页", Page(document, Color.Blue, 100));
            Raise(document, "OnEndPrint", new PrintEventArgs());
            Raise(document, "OnBeginPrint", new PrintEventArgs());
            check("Windows 重打同一文档从第一页开始", !Page(document, Color.Red, 100));
            Raise(document, "OnEndPrint", new PrintEventArgs());
        }

        var pdfContent = PrintFixtures.Pdf("1 0 0", "0 0 1");
        using (var pdf = new WindowsPdfDocument(pdfContent))
        {
            check("系统 PDF 引擎加载真实的两页文档", pdf.PageCount == 2);
            using var first = pdf.Render(0, 203);
            using var last = pdf.Render(1, 203);
            check("PDF 按生成 DPI 渲染且保留准确物理尺寸", first.Image.Width == 406 && first.Image.Height == 203
                && first.WidthInches == 2 && first.HeightInches == 1);
            check("PDF 两页内容独立且顺序正确", Pixel(first) == Color.Red.ToArgb() && Pixel(last) == Color.Blue.ToArgb());
        }

        var pdfFile = PrintFixtures.File(pdfContent, LabelFormat.Pdf);
        using (var document = WindowsLabelPrinter.CreateDocument(pdfFile))
        {
            document.OriginAtMargins = true;
            Raise(document, "OnBeginPrint", new PrintEventArgs());
            check("PDF 通过同一打印文档绘制第一页", !Page(document, Color.Red, 200));
            check("PDF 通过同一打印文档绘制第二页并结束", Page(document, Color.Blue, 200));
            Raise(document, "OnEndPrint", new PrintEventArgs());
        }

        var pdfZip = PrintFixtures.File(PrintFixtures.Zip(("z.pdf", pdfContent),
            ("a.pdf", PrintFixtures.Pdf("0 1 0"))), LabelFormat.Pdf, true);
        using (var pages = PrintPages.Read(pdfZip).GetEnumerator())
        {
            check("PDF ZIP 第一份文件的第一页", pages.MoveNext() && Pixel(pages.Current) == Color.Red.ToArgb());
            check("PDF ZIP 第一份文件的第二页", pages.MoveNext() && Pixel(pages.Current) == Color.Blue.ToArgb());
            check("PDF ZIP 第二份文件接在同一页序列后", pages.MoveNext() && Pixel(pages.Current) == Color.Lime.ToArgb());
            check("PDF ZIP 实际页数来自文档内容", !pages.MoveNext());
        }

        using (var pages = PrintPages.Read(pdfZip).GetEnumerator())
        {
            check("PDF 枚举可在第一页提前停止并释放资源", pages.MoveNext());
        }

        using (var document = WindowsLabelPrinter.CreateDocument(pdfFile))
        {
            Raise(document, "OnBeginPrint", new PrintEventArgs());
            // 模拟异常或取消后直接释放文档，不依赖 EndPrint 一定触发。
        }

        check("取消 PDF 后还能重新打开并渲染", FirstPdfPixel(pdfContent) == Color.Red.ToArgb());
        var closed = new WindowsPdfDocument(pdfContent);
        closed.Dispose();
        closed.Dispose();
        check("PDF 释放后拒绝继续渲染，支持重复释放", Throws<ObjectDisposedException>(() => closed.Render(0, 100)));
        check("损坏 PDF 向调用方报告原生错误", Throws<COMException>(() => new WindowsPdfDocument(new byte[] { 1, 2, 3 })));
        check("ZPL 不冒充图形打印文档", Throws<ArgumentException>(() => WindowsLabelPrinter.CreateDocument(
            PrintFixtures.File(new byte[] { 1 }, LabelFormat.Zpl))));
        check("Windows 打印拒绝空内容和零 DPI", Throws<ArgumentException>(() => WindowsLabelPrinter.CreateDocument(
            PrintFixtures.File([], LabelFormat.Png))) && Throws<ArgumentException>(() => WindowsLabelPrinter.CreateDocument(
                PrintFixtures.File(png.Content, LabelFormat.Png, dpi: 0))));
        var mixed = PrintFixtures.File(PrintFixtures.Zip(("first.png", png.Content), ("bad.pdf", pdfContent)), LabelFormat.Png, true);
        using (var pages = PrintPages.Read(mixed).GetEnumerator())
        {
            check("ZIP 混入其他格式时在打印第一页前失败", Throws<InvalidDataException>(() => pages.MoveNext()));
        }

        using (var page = PrintablePage.FromPng(png.Content, 200))
        using (var canvas = new Bitmap(300, 150))
        {
            canvas.SetResolution(100, 100);
            using var graphics = Graphics.FromImage(canvas);
            graphics.Clear(Color.White);
            var unit = graphics.PageUnit;
            WindowsLabelPrinter.DrawPage(graphics, page, 0, 0);
            check("PNG 用生成 DPI 还原尺寸，不自动铺满纸张", canvas.GetPixel(40, 10).ToArgb() == Color.Red.ToArgb()
                && canvas.GetPixel(70, 10).ToArgb() == Color.White.ToArgb());
            check("打印绘制后恢复 Graphics 状态", graphics.PageUnit == unit);
        }

        using (var files = PrintPages.Files(PrintFixtures.File(PrintFixtures.Zip(("z.zpl", new byte[] { 1 }),
            ("a.zpl", new byte[] { 2 })), LabelFormat.Zpl, true)).GetEnumerator())
        {
            check("ZPL ZIP 按服务顺序读取原始字节", files.MoveNext() && files.Current[0] == 1
                && files.MoveNext() && files.Current[0] == 2 && !files.MoveNext());
        }
#else
        Console.WriteLine("SKIP: 非 Windows 系统，跳过 Windows 桌面打印检查。");
#endif
    }

#if WINDOWS
    /// <summary>
    /// 读取已渲染页的中心颜色。
    /// </summary>
    /// <param name="page">页面。</param>
    /// <returns>中心颜色的 ARGB 值。</returns>
    private static int Pixel(PrintablePage page) => ((Bitmap)page.Image).GetPixel(page.Image.Width / 2, page.Image.Height / 2).ToArgb();

    /// <summary>
    /// 重新加载并渲染 PDF 第一页，用于确认提前停止后的资源释放。
    /// </summary>
    /// <param name="content">PDF。</param>
    /// <returns>第一页面的中心颜色。</returns>
    private static int FirstPdfPixel(byte[] content)
    {
        using var pdf = new WindowsPdfDocument(content);
        using var page = pdf.Render(0, 100);
        return Pixel(page);
    }

    /// <summary>
    /// 在内存中绘制一页，不调用实际打印控制器。
    /// </summary>
    /// <param name="document">文档。</param>
    /// <param name="color">预期页面颜色。</param>
    /// <param name="width">预期实际尺寸对应的一百 DPI 像素宽度。</param>
    /// <returns>最后一页时为真。</returns>
    private static bool Page(PrintDocument document, Color color, int width)
    {
        using var image = new Bitmap(400, 200);
        image.SetResolution(100, 100);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.White);
        var bounds = new Rectangle(0, 0, 400, 200);
        var args = new PrintPageEventArgs(graphics, bounds, bounds, document.DefaultPageSettings);
        Raise(document, "OnPrintPage", args);
        if (image.GetPixel(25, 20).ToArgb() != color.ToArgb() || image.GetPixel(width + 20, 20).ToArgb() != Color.White.ToArgb())
        {
            throw new Exception("打印页面的颜色或物理尺寸不正确。");
        }

        return !args.HasMorePages;
    }

    /// <summary>
    /// 触发文档分页事件，整个检查不执行 Print。
    /// </summary>
    /// <param name="document">文档。</param>
    /// <param name="method">事件触发方法。</param>
    /// <param name="args">事件参数。</param>
    private static void Raise(PrintDocument document, string method, EventArgs args) =>
        typeof(PrintDocument).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(document, [args]);

    /// <summary>
    /// 判断操作是否抛出指定异常，不吞掉其他程序错误。
    /// </summary>
    /// <typeparam name="T">预期异常。</typeparam>
    /// <param name="operation">操作。</param>
    /// <returns>预期异常发生时为真。</returns>
    private static bool Throws<T>(Action operation) where T : Exception
    {
        try
        {
            operation();
            return false;
        }
        catch (T)
        {
            return true;
        }
    }
#endif
}
