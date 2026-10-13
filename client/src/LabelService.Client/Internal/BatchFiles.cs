using System.IO.Compression;

namespace LabelService.Client.Internal;

/// <summary>
/// 读取已生成的批量内容，在 SDK 内维护业务条目、份数与纸页的对应关系。
/// </summary>
internal static class BatchFiles
{
    /// <summary>
    /// 解压时的总字节数上限，防止异常压缩包在客户端无限分配内存。
    /// </summary>
    private const long MaxExpandedBytes = 256L * 1024 * 1024;

    /// <summary>
    /// 复制请求及份数，以同一个快照发送和解释结果，不改变调用方的请求。
    /// </summary>
    /// <param name="source">调用方请求。</param>
    /// <returns>使用 ZIP 输出的独立请求。</returns>
    internal static BatchRenderRequest CopyRequest(BatchRenderRequest source)
    {
        var copy = new BatchRenderRequest(source.TemplateCode, source.Format)
        {
            Version = source.Version,
            Dpi = source.Dpi,
            Mono = source.Mono,
            Output = BatchOutput.Zip,
            Layout = source.Layout,
            StartSlot = source.StartSlot,
        };
        foreach (var value in source.Common)
        {
            copy.Common[value.Key] = CopyValue(value.Value);
        }
        foreach (var item in source.Items)
        {
            if (item == null)
            {
                throw new ArgumentException("批量条目不能为空。", nameof(source));
            }
            var next = copy.AddItem(item.Copies);
            foreach (var value in item.Data)
            {
                next.Data[value.Key] = CopyValue(value.Value);
            }
        }
        return copy;
    }

    /// <summary>
    /// 保留原文件，按请求快照分组条目或构造拼版页引用。
    /// </summary>
    /// <param name="file">原始生成结果。</param>
    /// <param name="request">发送给服务的请求快照。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可打印集合。</returns>
    internal static LabelBatchResult Read(LabelFile file, BatchRenderRequest request, CancellationToken cancellationToken)
    {
        if (file.Format != request.Format || file.Dpi <= 0 || file.LabelCount != request.Items.Sum(item => item.Copies))
        {
            throw Invalid("批量响应的格式、DPI 或标签张数无效。", file);
        }
        var sheet = !string.IsNullOrWhiteSpace(request.Layout);
        var items = new List<LabelBatchItem>();
        if (sheet && file.Format == LabelFormat.Pdf && !file.IsZip)
        {
            if (file.PageCount <= 0 || file.PageCount > 500)
            {
                throw Invalid("拼版响应的页数无效。", file);
            }
            for (var index = 0; index < file.PageCount; index++)
            {
                items.Add(new LabelBatchItem(Array.AsReadOnly(new[] { file }), null, index + 1, 1, index));
            }
        }
        else
        {
            if (sheet && file.Format != LabelFormat.Png)
            {
                throw Invalid("拼版集合只支持 PNG 或 PDF。", file);
            }
            var expected = sheet ? file.PageCount : request.Format == LabelFormat.Png
                ? request.Items.Sum(item => item.Copies) : request.Items.Count;
            var parts = Unpack(file, expected, sheet, cancellationToken);
            var offset = 0;
            var count = sheet ? expected : request.Items.Count;
            for (var index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var copies = sheet ? 1 : request.Items[index].Copies;
                var fileCount = !sheet && request.Format == LabelFormat.Png ? copies : 1;
                var group = new List<LabelFile>();
                for (var part = 0; part < fileCount; part++)
                {
                    var next = parts[offset++];
                    // 分项没有独立服务计时；只复制生成参数和追踪信息，不把整批耗时误标为分项耗时。
                    next.TemplateVersion = file.TemplateVersion;
                    next.Dpi = file.Dpi;
                    next.TraceId = file.TraceId;
                    next.RequestId = file.RequestId;
                    next.Warnings = file.Warnings;
                    next.LabelCount = sheet ? 0 : request.Format == LabelFormat.Png ? 1 : copies;
                    next.PageCount = sheet || request.Format == LabelFormat.Png ? 1 : copies;
                    group.Add(next);
                }
                items.Add(new LabelBatchItem(group.AsReadOnly(), sheet ? (int?)null : index + 1,
                    sheet ? index + 1 : (int?)null, copies, null));
            }
        }
        return new LabelBatchResult(file, sheet ? LabelBatchUnit.Page : LabelBatchUnit.Item, items.AsReadOnly());
    }

    /// <summary>
    /// 按 ZIP 条目原顺序读取文件，核对后缀、数量、大小和重复名称，不向磁盘解压。
    /// </summary>
    /// <param name="file">服务响应。</param>
    /// <param name="expected">按契约应有的文件数量。</param>
    /// <param name="sheet">是否为拼版；只有单页拼版 PNG 可以不使用 ZIP。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>单个文件的列表。</returns>
    private static List<LabelFile> Unpack(LabelFile file, int expected, bool sheet, CancellationToken cancellationToken)
    {
        if (expected <= 0 || expected > 500)
        {
            throw Invalid("批量响应的文件数量无效。", file);
        }
        if (!file.IsZip)
        {
            // 单页拼版 PNG 直接返回 PNG，而非 ZIP。
            if (sheet && expected == 1 && file.Format == LabelFormat.Png)
            {
                return new List<LabelFile> { new LabelFile(file.Content, file.ContentType, file.FileName, file.Format) };
            }
            throw Invalid("批量集合响应应为 ZIP。", file);
        }
        try
        {
            using (var stream = file.OpenRead())
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var entries = zip.Entries.Where(entry => entry.Name.Length > 0).ToArray();
                var extension = file.Format == LabelFormat.Png ? ".png" : file.Format == LabelFormat.Pdf ? ".pdf" : ".zpl";
                var type = file.Format == LabelFormat.Png ? "image/png" : file.Format == LabelFormat.Pdf ? "application/pdf" : "text/plain; charset=utf-8";
                if (entries.Length != expected || entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() != expected
                    || entries.Any(entry => !entry.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || entry.Length <= 0)
                    || entries.Sum(entry => entry.Length) > MaxExpandedBytes)
                {
                    throw Invalid("批量 ZIP 的文件数量、类型或大小不符合预期。", file);
                }
                var files = new List<LabelFile>();
                var buffer = new byte[81920];
                long expanded = 0;
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var input = entry.Open())
                    using (var content = new MemoryStream())
                    {
                        int length;
                        while ((length = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            expanded += length;
                            if (expanded > MaxExpandedBytes || content.Length + length > entry.Length)
                            {
                                throw Invalid("批量 ZIP 的实际解压大小超出预期。", file);
                            }
                            content.Write(buffer, 0, length);
                        }
                        if (content.Length != entry.Length)
                        {
                            throw Invalid("批量 ZIP 的文件长度不完整。", file);
                        }
                        files.Add(new LabelFile(content.ToArray(), type, entry.Name, file.Format));
                    }
                }
                return files;
            }
        }
        catch (InvalidDataException exception)
        {
            throw Invalid("无法读取批量 ZIP：" + exception.Message, file);
        }
    }

    /// <summary>
    /// 复制可变的图片字节，其余允许的数据值均为不可变标量。
    /// </summary>
    /// <param name="value">字段值。</param>
    /// <returns>独立图片或原标量。</returns>
    private static object? CopyValue(object? value) => value is byte[] bytes ? bytes.Clone() : value;

    /// <summary>
    /// 构造异常响应错误并保留原调用的追踪信息。
    /// </summary>
    /// <param name="message">说明。</param>
    /// <param name="file">服务响应。</param>
    /// <returns>可追踪的 SDK 异常。</returns>
    private static LabelServiceException Invalid(string message, LabelFile file) =>
        new LabelServiceException(LabelErrorCodes.UnexpectedResponse, message) { TraceId = file.TraceId, RequestId = file.RequestId };
}
