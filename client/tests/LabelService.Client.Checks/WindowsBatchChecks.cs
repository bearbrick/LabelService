#if WINDOWS
using System.Drawing;
using System.Drawing.Printing;
using System.Reflection;
using System.Text;
using LabelService.Client.Internal;
using LabelService.Client.Printing;
#endif

namespace LabelService.Client.Checks;

/// <summary>
/// 集合打印检查，在内存页面验证单项、多份、重排子集和 PDF 纸页，不提交真实打印。
/// </summary>
internal static class WindowsBatchChecks
{
    /// <summary>
    /// 运行桌面集合检查。
    /// </summary>
    /// <param name="check">记录结果。</param>
    public static void Run(Action<string, bool> check)
    {
#if WINDOWS
        var request = new BatchRenderRequest("A", LabelFormat.Png);
        request.AddItem();
        request.AddItem(2);
        var png = Result(PrintFixtures.Zip(("z.png", PrintFixtures.Png(Color.Red)),
            ("a.png", PrintFixtures.Png(Color.Blue)), ("b.png", PrintFixtures.Png(Color.Blue))), request);
        check("单项 PNG 打印保留该条的两份", Colors(png.Items[1]).SequenceEqual(new[] { Color.Blue.ToArgb(), Color.Blue.ToArgb() }));
        check("PNG 整批通过一个文档连续分页", Colors(png.Items).SequenceEqual(new[]
            { Color.Red.ToArgb(), Color.Blue.ToArgb(), Color.Blue.ToArgb() }));
        check("PNG 子集可重排、重复选择，不重新请求服务", Colors(new[] { png.Items[1], png.Items[0], png.Items[1] }).SequenceEqual(new[]
            { Color.Blue.ToArgb(), Color.Blue.ToArgb(), Color.Red.ToArgb(), Color.Blue.ToArgb(), Color.Blue.ToArgb() }));

        var pdfRequest = new BatchRenderRequest("A", LabelFormat.Pdf);
        pdfRequest.AddItem();
        pdfRequest.AddItem(2);
        var pdf = Result(PrintFixtures.Zip(("z.pdf", PrintFixtures.Pdf("1 0 0")),
            ("a.pdf", PrintFixtures.Pdf("0 0 1", "0 1 0"))), pdfRequest);
        check("PDF 业务条目保留内部多份页，整批连续打印", Colors(pdf.Items).SequenceEqual(new[]
            { Color.Red.ToArgb(), Color.Blue.ToArgb(), Color.Lime.ToArgb() }));
        check("选择 PDF 第二条只打印该条的全部份数", Colors(pdf.Items[1]).SequenceEqual(new[] { Color.Blue.ToArgb(), Color.Lime.ToArgb() }));
        pdfRequest.Layout = "A4";
        var original = PrintFixtures.File(PrintFixtures.Pdf("1 0 0", "0 0 1"), LabelFormat.Pdf, dpi: 203);
        original.PageCount = 2;
        original.LabelCount = 3;
        var sheet = BatchFiles.Read(original, pdfRequest, CancellationToken.None);
        check("PDF 拼版第二页引用原文档，只打印选中的蓝色页", Colors(sheet.Items[1]).SequenceEqual(new[] { Color.Blue.ToArgb() }));
        check("PDF 拼版可按选择顺序连续打印和重复补打", Colors(new[] { sheet.Items[1], sheet.Items[0], sheet.Items[1] })
            .SequenceEqual(new[] { Color.Blue.ToArgb(), Color.Red.ToArgb(), Color.Blue.ToArgb() }));
        using (var document = WindowsLabelPrinter.CreateDocument(sheet.Items[1]))
        {
            document.PrinterSettings.Copies = 2;
            check("集合图形文档可在打印前调整参数，保持静默控制器", document.PrintController is StandardPrintController
                && document.PrinterSettings.Copies == 2);
        }
        var rawRequest = new BatchRenderRequest("A", LabelFormat.Zpl);
        rawRequest.AddItem();
        rawRequest.AddItem(2);
        var raw = Result(PrintFixtures.Zip(("z.zpl", Encoding.ASCII.GetBytes("^XA^FO1^XZ")),
            ("a.zpl", Encoding.ASCII.GetBytes("^XA^PQ2^XZ"))), rawRequest);
        check("ZPL 子集一次合并原指令，不丢失份数、不重复添加份数", Encoding.ASCII.GetString(
            WindowsLabelPrinter.RawContent(new[] { raw.Items[1], raw.Items[0] })) == "^XA^PQ2^XZ^XA^FO1^XZ");
        check("空集合和混合 RAW/图形在提交前被拒绝", Throws<ArgumentException>(() => WindowsLabelPrinter.CreateDocument(Array.Empty<LabelBatchItem>()))
            && Throws<ArgumentException>(() => WindowsLabelPrinter.CreateDocument(new[] { raw.Items[0], png.Items[0] })));
        var badPage = new LabelBatchItem(new[] { original }, null, 3, 1, 2);
        check("不存在的 PDF 页在创建文档时被拒绝", Throws<ArgumentOutOfRangeException>(() => WindowsLabelPrinter.CreateDocument(badPage)));
        check("提前停止 PDF 枚举后可再次打印选中页", FirstColor(sheet.Items[1]) == Color.Blue.ToArgb()
            && FirstColor(sheet.Items[1]) == Color.Blue.ToArgb());
#else
        Console.WriteLine("SKIP: 非 Windows 系统，跳过集合桌面打印检查。");
#endif
    }

#if WINDOWS
    /// <summary>
    /// 构造实际图形内容的业务集合。
    /// </summary>
    /// <param name="content">压缩内容。</param>
    /// <param name="request">对应请求。</param>
    /// <returns>集合。</returns>
    private static LabelBatchResult Result(byte[] content, BatchRenderRequest request)
    {
        var file = PrintFixtures.File(content, request.Format, true);
        file.LabelCount = request.Items.Sum(item => item.Copies);
        return BatchFiles.Read(file, request, CancellationToken.None);
    }

    /// <summary>
    /// 验证单项重载与集合重载行为相同。
    /// </summary>
    /// <param name="item">单项。</param>
    /// <returns>各页颜色。</returns>
    private static int[] Colors(LabelBatchItem item)
    {
        using var document = WindowsLabelPrinter.CreateDocument(item);
        return Colors(document);
    }

    /// <summary>
    /// 在同一文档内绘制指定集合。
    /// </summary>
    /// <param name="items">选择。</param>
    /// <returns>各页颜色。</returns>
    private static int[] Colors(IEnumerable<LabelBatchItem> items)
    {
        using var document = WindowsLabelPrinter.CreateDocument(items);
        return Colors(document);
    }

    /// <summary>
    /// 只触发分页事件到内存画布，完全不调用实际 Print。
    /// </summary>
    /// <param name="document">文档。</param>
    /// <returns>各页中心颜色。</returns>
    private static int[] Colors(PrintDocument document)
    {
        document.OriginAtMargins = true;
        var colors = new List<int>();
        Raise(document, "OnBeginPrint", new PrintEventArgs());
        try
        {
            bool more;
            do
            {
                using var image = new Bitmap(400, 200);
                image.SetResolution(100, 100);
                using var graphics = Graphics.FromImage(image);
                graphics.Clear(Color.White);
                var bounds = new Rectangle(0, 0, 400, 200);
                var args = new PrintPageEventArgs(graphics, bounds, bounds, document.DefaultPageSettings);
                Raise(document, "OnPrintPage", args);
                colors.Add(image.GetPixel(25, 20).ToArgb());
                more = args.HasMorePages;
            } while (more && colors.Count < 20);
        }
        finally { Raise(document, "OnEndPrint", new PrintEventArgs()); }
        return colors.ToArray();
    }

    /// <summary>
    /// 触发打印文档的内部事件。
    /// </summary>
    /// <param name="document">文档。</param>
    /// <param name="method">事件方法。</param>
    /// <param name="args">参数。</param>
    private static void Raise(PrintDocument document, string method, EventArgs args) =>
        typeof(PrintDocument).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(document, new object[] { args });

    /// <summary>
    /// 提前结束枚举，确认资源能释放。
    /// </summary>
    /// <param name="item">纸页。</param>
    /// <returns>颜色。</returns>
    private static int FirstColor(LabelBatchItem item)
    {
        using var pages = BatchPrintPages.Read(new[] { item }).GetEnumerator();
        if (!pages.MoveNext())
        {
            throw new Exception("预期页不存在。");
        }
        return ((Bitmap)pages.Current.Image).GetPixel(10, 10).ToArgb();
    }

    /// <summary>
    /// 检查指定异常。
    /// </summary>
    /// <typeparam name="T">异常类型。</typeparam>
    /// <param name="action">操作。</param>
    /// <returns>是否符合。</returns>
    private static bool Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T) { return true; }
    }
#endif
}
