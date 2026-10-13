using System;
using System.Collections.Generic;
using System.IO;

namespace LabelService.Client.Printing;

/// <summary>
/// 集合的页面读取，按项保留 PNG 份数和 PDF 页选择，共用原 PDF 文档，不生成临时文件。
/// </summary>
internal static class BatchPrintPages
{
    /// <summary>
    /// 在打印前核对全部 PNG、PDF 内容及页号；同一 PDF 只加载一次用于验证。
    /// </summary>
    /// <param name="items">打印选择。</param>
    internal static void Validate(IEnumerable<LabelBatchItem> items)
    {
        var documents = new Dictionary<LabelFile, WindowsPdfDocument>();
        try
        {
            foreach (var item in items)
            {
                foreach (var file in item.Files)
                {
                    if (file.Format == LabelFormat.Png)
                    {
                        if (item.PdfPageIndex.HasValue)
                        {
                            throw new InvalidDataException("PNG 不能使用 PDF 页选择。");
                        }
                        using (var page = PrintablePage.FromPng(file.Content, file.Dpi))
                        {
                        }
                    }
                    else
                    {
                        var pdf = Document(file, documents);
                        if (item.PdfPageIndex.HasValue && (item.PdfPageIndex.Value < 0 || item.PdfPageIndex.Value >= pdf.PageCount))
                        {
                            throw new ArgumentOutOfRangeException(nameof(items), "所选 PDF 页号超出实际页数。");
                        }
                        if (file.PageCount > 0 && file.PageCount != pdf.PageCount)
                        {
                            throw new InvalidDataException("PDF 实际页数和生成元数据不一致。");
                        }
                    }
                }
            }
        }
        finally
        {
            foreach (var pdf in documents.Values)
            {
                pdf.Dispose();
            }
        }
    }

    /// <summary>
    /// 按传入顺序懒读取一页，PDF 普通条目打印全部份数，拼版只渲染所选纸页。
    /// </summary>
    /// <param name="items">完整集合或子集。</param>
    /// <returns>实际应提交的页面序列，枚举器释放时关闭原生文档。</returns>
    internal static IEnumerable<PrintablePage> Read(IEnumerable<LabelBatchItem> items)
    {
        var documents = new Dictionary<LabelFile, WindowsPdfDocument>();
        try
        {
            foreach (var item in items)
            {
                foreach (var file in item.Files)
                {
                    if (file.Format == LabelFormat.Png)
                    {
                        using (var page = PrintablePage.FromPng(file.Content, file.Dpi))
                        {
                            yield return page;
                        }
                    }
                    else
                    {
                        var pdf = Document(file, documents);
                        var first = item.PdfPageIndex ?? 0;
                        var count = item.PdfPageIndex.HasValue ? 1 : pdf.PageCount;
                        for (var index = first; index < first + count; index++)
                        {
                            using (var page = pdf.Render(index, file.Dpi))
                            {
                                yield return page;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            foreach (var pdf in documents.Values)
            {
                pdf.Dispose();
            }
        }
    }

    /// <summary>
    /// 同一次枚举中重复选中原 PDF 的页面时复用已加载文档。
    /// </summary>
    /// <param name="file">原文件。</param>
    /// <param name="documents">本次枚举拥有的原生文档。</param>
    /// <returns>已加载文档。</returns>
    private static WindowsPdfDocument Document(LabelFile file, IDictionary<LabelFile, WindowsPdfDocument> documents)
    {
        if (!documents.TryGetValue(file, out var pdf))
        {
            pdf = new WindowsPdfDocument(file.Content);
            documents.Add(file, pdf);
        }
        return pdf;
    }
}
