using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace LabelService.Client.Printing;

/// <summary>
/// 服务文件的按需分页读取：ZIP 保持服务给出的顺序，每次只保留当前页面。
/// </summary>
internal static class PrintPages
{
    /// <summary>
    /// 读取 PNG 或 PDF 的全部页面，枚举器提前释放时关闭当前页面和 PDF 文档。
    /// </summary>
    /// <param name="file">服务生成结果。</param>
    /// <returns>按顺序枚举的可打印页面。</returns>
    internal static IEnumerable<PrintablePage> Read(LabelFile file)
    {
        foreach (var content in Files(file))
        {
            if (file.Format == LabelFormat.Png)
            {
                using (var page = PrintablePage.FromPng(content, file.Dpi))
                {
                    yield return page;
                }
            }
            else
            {
                using (var pdf = new WindowsPdfDocument(content))
                {
                    for (var index = 0; index < pdf.PageCount; index++)
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

    /// <summary>
    /// 解开当前文件；ZIP 先核对全部条目的后缀，避免打印到一半才发现混入其他格式。
    /// </summary>
    /// <param name="file">服务生成结果。</param>
    /// <returns>原文件或 ZIP 条目内容。</returns>
    internal static IEnumerable<byte[]> Files(LabelFile file)
    {
        if (!file.IsZip)
        {
            yield return file.Content;
            yield break;
        }

        var extension = file.Format == LabelFormat.Png ? ".png" : file.Format == LabelFormat.Pdf ? ".pdf" : ".zpl";
        using (var source = file.OpenRead())
        using (var zip = new ZipArchive(source, ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.Name.Length > 0 && !entry.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("打印压缩包中包含不匹配的文件：" + entry.FullName);
                }
            }

            foreach (var entry in zip.Entries)
            {
                if (entry.Name.Length == 0)
                {
                    continue;
                }

                using (var stream = entry.Open())
                using (var content = new MemoryStream())
                {
                    stream.CopyTo(content);
                    yield return content.ToArray();
                }
            }
        }
    }
}
