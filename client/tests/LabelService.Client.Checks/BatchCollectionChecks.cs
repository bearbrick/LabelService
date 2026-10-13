using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using LabelService.Client.Internal;

namespace LabelService.Client.Checks;

/// <summary>
/// 集合接口检查：一次请求、业务分组、原字节、份数、拼版页和异常响应。
/// </summary>
internal static class BatchCollectionChecks
{
    /// <summary>
    /// 检查三个格式和拼版集合，不依赖打印设备。
    /// </summary>
    /// <param name="check">记录结果。</param>
    /// <returns>检查完成时结束。</returns>
    public static async Task RunAsync(Action<string, bool> check)
    {
        foreach (var format in new[] { LabelFormat.Png, LabelFormat.Pdf, LabelFormat.Zpl })
        {
            var request = Request(format);
            var content = Zip(format, format == LabelFormat.Png ? 3 : 2);
            using var handler = new Handler(content);
            using var implementation = new LabelServiceClient(new LabelServiceClientOptions
            {
                BaseAddress = new Uri("http://example.invalid/"),
                ApiKey = "test-key",
                MaxRetries = 0
            }, handler);
            ILabelServiceClient client = implementation;
            var completed = 0;
            client.CallCompleted += (_, _) => completed++;
            var result = await client.RenderBatchFilesAsync(request);
            check($"{format} 集合只调用一次并强制 ZIP，不改变原请求",
                handler.Calls == 1 && completed == 1 && handler.Body.Contains("\"output\":\"zip\"")
                && request.Output == BatchOutput.Merge && result.OriginalFile.Content.SequenceEqual(content));
            check($"{format} 按业务条目分组并保留原顺序及多份",
                result.Unit == LabelBatchUnit.Item && result.Items.Count == 2
                && result.Items[0].ItemNumber == 1 && result.Items[1].ItemNumber == 2 && result.Items[1].Copies == 2
                && result.Items[1].PageNumber == null && result.Items[1].Files.Count == (format == LabelFormat.Png ? 2 : 1)
                && result.Items[0].Files[0].Content.SequenceEqual(new byte[] { 1 })
                && result.Items[1].Files[0].Content.SequenceEqual(new byte[] { 2 }));
            check($"{format} 分项保留实际版本、DPI 和追踪信息，整批计时只留在原文件",
                result.Items.SelectMany(item => item.Files).All(file => file.TemplateVersion == 7 && file.Dpi == 203
                    && file.TraceId == "batch-trace" && file.ServerDuration == null));
            check($"{format} 同步扩展兼容原接口", client.RenderBatchFiles(request).Items.Count == 2);
        }

        var sheet = Request(LabelFormat.Pdf);
        sheet.Layout = "A4";
        var pdf = File(new byte[] { 1 }, LabelFormat.Pdf, false, 2);
        var pages = BatchFiles.Read(pdf, sheet, CancellationToken.None);
        check("PDF 拼版按页组织，两页引用同一个原 PDF 而非重新生成",
            pages.Unit == LabelBatchUnit.Page && pages.Items.Count == 2 && pages.Items[1].PageNumber == 2
            && pages.Items[1].PdfPageIndex == 1 && pages.Items[1].ItemNumber == null
            && ReferenceEquals(pages.Items[0].Files[0], pdf) && ReferenceEquals(pages.Items[1].Files[0], pdf));
        sheet = Request(LabelFormat.Png);
        sheet.Layout = "A4";
        var png = BatchFiles.Read(File(Zip(LabelFormat.Png, 2), LabelFormat.Png, true, 2), sheet, CancellationToken.None);
        check("PNG 拼版按纸页选择，不按业务条目或份数拆分", png.Items.Count == 2 && png.Items[1].Copies == 1
            && png.Items[1].Files[0].PageCount == 1 && png.Items[1].Files[0].LabelCount == 0);
        check("单页 PNG 拼版可直接返回 PNG", BatchFiles.Read(File(new byte[] { 1 }, LabelFormat.Png, false, 1), sheet,
            CancellationToken.None).Items.Count == 1);
        var source = Request(LabelFormat.Png);
        var bytes = new byte[] { 9 };
        source.Common["logo"] = bytes;
        var snapshot = BatchFiles.CopyRequest(source);
        bytes[0] = 8;
        source.Items[1].Copies = 1;
        check("请求快照固定图片及条目份数，后续修改不改变分组", ((byte[])snapshot.Common["logo"]!)[0] == 9
            && snapshot.Items[1].Copies == 2);
        var normal = Request(LabelFormat.Png);
        check("普通集合拒绝非 ZIP 和缺少文件", Invalid(File(new byte[] { 1 }, LabelFormat.Png), normal)
            && Invalid(File(Zip(LabelFormat.Png, 2), LabelFormat.Png, true), normal));
        var wrongCount = File(Zip(LabelFormat.Png, 3), LabelFormat.Png, true);
        wrongCount.LabelCount = 2;
        var zeroDpi = File(Zip(LabelFormat.Png, 3), LabelFormat.Png, true);
        zeroDpi.Dpi = 0;
        check("集合拒绝损坏 ZIP、错误格式、错误张数和零 DPI", Invalid(File(new byte[] { 1 }, LabelFormat.Png, true), normal)
            && Invalid(File(Zip(LabelFormat.Pdf, 3), LabelFormat.Png, true), normal)
            && Invalid(wrongCount, normal) && Invalid(zeroDpi, normal));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        check("集合解压尊重取消", Throws<OperationCanceledException>(() => BatchFiles.Read(
            File(Zip(LabelFormat.Png, 3), LabelFormat.Png, true), normal, canceled.Token)));
    }

    /// <summary>
    /// 创建两条共三份的请求。
    /// </summary>
    /// <param name="format">格式。</param>
    /// <returns>请求。</returns>
    private static BatchRenderRequest Request(LabelFormat format)
    {
        var request = new BatchRenderRequest("BOX_LABEL", format) { Output = BatchOutput.Merge };
        request.AddItem().Set("boxNo", "A");
        request.AddItem(2).Set("boxNo", "B");
        return request;
    }

    /// <summary>
    /// 创建顺序刻意不同于名称排序的压缩包。
    /// </summary>
    /// <param name="format">后缀。</param>
    /// <param name="count">文件数量。</param>
    /// <returns>压缩内容。</returns>
    private static byte[] Zip(LabelFormat format, int count)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            for (var index = 0; index < count; index++)
            {
                using var entry = zip.CreateEntry((count - index) + "." + format.ToString().ToLowerInvariant()).Open();
                entry.WriteByte((byte)(index + 1));
            }
        }
        return stream.ToArray();
    }

    /// <summary>
    /// 创建带必要元数据的假服务结果。
    /// </summary>
    /// <param name="content">内容。</param>
    /// <param name="format">格式。</param>
    /// <param name="zip">是否压缩。</param>
    /// <param name="pages">纸页数。</param>
    /// <returns>结果。</returns>
    private static LabelFile File(byte[] content, LabelFormat format, bool zip = false, int pages = 3) =>
        new(content, zip ? "application/zip" : "image/png", "batch", format)
        {
            Dpi = 203,
            TemplateVersion = 7,
            LabelCount = 3,
            PageCount = pages
        };

    /// <summary>
    /// 判断坏结果是否映射为可追踪的 SDK 异常。
    /// </summary>
    /// <param name="file">结果。</param>
    /// <param name="request">请求。</param>
    /// <returns>异常是否符合预期。</returns>
    private static bool Invalid(LabelFile file, BatchRenderRequest request) => Throws<LabelServiceException>(() =>
        BatchFiles.Read(file, request, CancellationToken.None));

    /// <summary>
    /// 检查操作是否抛出指定异常。
    /// </summary>
    /// <typeparam name="T">异常类型。</typeparam>
    /// <param name="action">操作。</param>
    /// <returns>是否抛出。</returns>
    private static bool Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T) { return true; }
    }

    /// <summary>
    /// 记录请求并返回同一批次内容的 HTTP 假服务。
    /// </summary>
    private sealed class Handler : HttpMessageHandler
    {
        /// <summary>
        /// 压缩内容。
        /// </summary>
        private readonly byte[] content;
        /// <summary>
        /// 请求次数。
        /// </summary>
        public int Calls
        {
            get; private set;
        }
        /// <summary>
        /// 最近的 JSON。
        /// </summary>
        public string Body { get; private set; } = "";
        /// <summary>
        /// 初始化固定响应。
        /// </summary>
        /// <param name="content">内容。</param>
        public Handler(byte[] content) => this.content = content;
        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            response.Headers.Add("X-Template-Version", "7");
            response.Headers.Add("X-Label-Count", "3");
            response.Headers.Add("X-Label-Dpi", "203");
            response.Headers.Add("X-Trace-Id", "batch-trace");
            return response;
        }
    }
}
