using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;

namespace LabelService.Client.Printing;

/// <summary>
/// 按 SDK 返回的业务条目或拼版纸页提交单项、集合及子集。
/// </summary>
public static partial class WindowsLabelPrinter
{
    /// <summary>
    /// 静默打印一个业务条目的全部份数，或一页拼版纸张，不重新请求服务。
    /// </summary>
    /// <param name="item">集合中的一项。</param>
    /// <param name="printerName">打印机名称；不传或空字符串时用默认打印机。</param>
    public static void Print(LabelBatchItem item, string? printerName = null) =>
        Print(new[] { item }, printerName);

    /// <summary>
    /// 按传入顺序打印集合，图形页面使用一个打印文档，ZPL 使用一个 RAW 作业。
    /// </summary>
    /// <param name="items">整批或选中的子集，可包含重复项以明确补打；不得混合 ZPL 和图形格式。</param>
    /// <param name="printerName">打印机名称；不传时使用默认打印机。</param>
    public static void Print(IEnumerable<LabelBatchItem> items, string? printerName = null)
    {
        var selected = SelectItems(items);
        if (selected[0].Files[0].Format == LabelFormat.Zpl)
        {
            var content = RawContent(selected);
            var settings = new PrinterSettings();
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                settings.PrinterName = printerName;
            }
            var name = settings.PrinterName;
            if (name == null || name.Length == 0 || !settings.IsValid)
            {
                throw new InvalidPrinterException(settings);
            }
            WindowsRawPrinter.Send(name, content, "标签集合");
            return;
        }
        using (var document = CreateSelectedDocument(selected, printerName))
        {
            document.Print();
        }
    }

    /// <summary>
    /// 创建单项的图形打印文档，特殊参数可在 Print 前修改；ZPL 不支持此方法。
    /// </summary>
    /// <param name="item">业务条目或拼版纸页。</param>
    /// <param name="printerName">打印机名称。</param>
    /// <returns>需要由调用方释放的打印文档。</returns>
    public static PrintDocument CreateDocument(LabelBatchItem item, string? printerName = null) =>
        CreateDocument(new[] { item }, printerName);

    /// <summary>
    /// 创建整个图形集合或子集的分页打印文档，保留传入顺序。
    /// </summary>
    /// <param name="items">PNG、PDF 的业务条目或拼版页。</param>
    /// <param name="printerName">打印机名称。</param>
    /// <returns>需要由调用方释放的连续分页文档。</returns>
    public static PrintDocument CreateDocument(IEnumerable<LabelBatchItem> items, string? printerName = null) =>
        CreateSelectedDocument(SelectItems(items), printerName);

    /// <summary>
    /// 固定集合快照，预先拒绝空集合、无效内容、错误页号或混合 RAW 与图形的作业。
    /// </summary>
    /// <param name="items">调用方选择的集合。</param>
    /// <returns>已核对的有序快照。</returns>
    private static LabelBatchItem[] SelectItems(IEnumerable<LabelBatchItem> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }
        var selected = items.ToArray();
        if (selected.Length == 0 || selected.Any(item => item == null || item.Files.Count == 0))
        {
            throw new ArgumentException("请选择至少一个有效的打印项。", nameof(items));
        }
        foreach (var item in selected)
        {
            foreach (var file in item.Files)
            {
                Validate(file);
                if (file.IsZip)
                {
                    throw new ArgumentException("集合项应包含已由 SDK 拆出的文件。", nameof(items));
                }
            }
        }
        var raw = selected[0].Files[0].Format == LabelFormat.Zpl;
        if (selected.SelectMany(item => item.Files).Any(file => (file.Format == LabelFormat.Zpl) != raw))
        {
            throw new ArgumentException("一个打印作业不能混合 ZPL 和 PNG/PDF。", nameof(items));
        }
        if (raw && selected.Any(item => item.PdfPageIndex.HasValue))
        {
            throw new ArgumentException("ZPL 不支持 PDF 页选择。", nameof(items));
        }
        return selected;
    }

    /// <summary>
    /// 在打开打印队列前预先验证所有选中图形文件及页号，之后连续分页绘制。
    /// </summary>
    /// <param name="selected">已固定的选择。</param>
    /// <param name="printerName">打印机名称。</param>
    /// <returns>分页文档。</returns>
    private static PrintDocument CreateSelectedDocument(LabelBatchItem[] selected, string? printerName)
    {
        if (selected[0].Files[0].Format == LabelFormat.Zpl)
        {
            throw new ArgumentException("ZPL 使用原始指令，请调用 Print。", nameof(selected));
        }
        BatchPrintPages.Validate(selected);
        return CreateDocument(() => BatchPrintPages.Read(selected), printerName, "标签集合");
    }

    /// <summary>
    /// 将 ZPL 原始文件按选择顺序合并，保留每条的份数指令，不在条目间启动多个队列作业。
    /// </summary>
    /// <param name="selected">选中项。</param>
    /// <returns>待一次提交的原始字节。</returns>
    internal static byte[] RawContent(IEnumerable<LabelBatchItem> selected)
    {
        using (var content = new MemoryStream())
        {
            foreach (var file in selected.SelectMany(item => item.Files))
            {
                content.Write(file.Content, 0, file.Content.Length);
            }
            return content.ToArray();
        }
    }
}
