using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace LabelService.Backend.Checks;

/// <summary>
/// 批量接口：merge（PDF、ZPL）、zip（每条一个文件，PNG 每份一个文件）、顺序、先校验全部条目、错误带序号、上限、计时和张数头。
/// </summary>
internal static partial class BatchChecks
{
    /// <summary>
    /// 批量请求的公共字段：示例箱标除箱号以外的数据。
    /// </summary>
    private const string Common = """{"partName":"电源适配器","partNo":"3100-0123","qty":50,"batchNo":"20261008-A","prodDate":"2026-10-08"}""";

    /// <summary>
    /// 运行批量接口的全部检查：用临时数据目录启动服务。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory);
            using var http = factory.CreateClient();
            await CheckMergeAsync(http, check);
            await CheckZipAsync(http, check);
            await CheckErrorsAsync(http, check);
            await CheckPerformanceAsync(http, check);
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    /// <summary>
    /// merge：PDF 多页、ZPL 多段，顺序和份数正确，张数和计时头。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckMergeAsync(HttpClient http, Action<string, bool> check)
    {
        var pdf = await Post(http, Batch("pdf", "merge", Item("C2610080001"), Item("C2610080002", 2)));
        var pdfText = Encoding.Latin1.GetString(await pdf.Content.ReadAsByteArrayAsync());
        check("merge PDF：2 条（第 2 条 2 份）合成一个 3 页的 PDF",
            pdf.IsSuccessStatusCode && pdf.Content.Headers.ContentType?.MediaType == "application/pdf" && Regex.Count(pdfText, @"/Type\s*/Page(?![s\w])") == 3);
        check("X-Label-Count 是总张数（含份数），Server-Timing 有全部阶段", ServerChecks.Header(pdf, HeaderNames.LabelCount) == "3"
            && ServerChecks.Header(pdf, HeaderNames.ServerTiming)!.Split(", ").Select(item => item.Split(';')[0]).SequenceEqual(TimingStages.All));
        check("下载文件名是 模板编码_日期.pdf", pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"') is { } name && name.StartsWith("BOX_LABEL_") && name.EndsWith(".pdf"));

        var zpl = await Post(http, Batch("zpl", null, Item("C2610080001"), Item("C2610080002", 2)));
        var zplText = await zpl.Content.ReadAsStringAsync();
        check("merge 是默认值；ZPL 合并成一个文件，每条一个 ^XA…^XZ，份数各自用 ^PQ",
            zpl.IsSuccessStatusCode && Regex.Count(zplText, @"\^XA") == 2 && zplText.IndexOf("^PQ1", StringComparison.Ordinal) < zplText.IndexOf("^PQ2", StringComparison.Ordinal));
    }

    /// <summary>
    /// zip：每条一个文件、按顺序命名，PNG 每份一个文件，条码内容和条目一致。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckZipAsync(HttpClient http, Action<string, bool> check)
    {
        var response = await Post(http, Batch("png", "zip", Item("C2610080001"), Item("C2610080002", 2), Item("C2610080003")));
        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var names = zip.Entries.Select(entry => entry.FullName).ToArray();
        check("zip：PNG 每份一个文件，文件名 模板编码_序号.png，Content-Type 是 application/zip",
            response.Content.Headers.ContentType?.MediaType == "application/zip"
            && names.SequenceEqual(["BOX_LABEL_001.png", "BOX_LABEL_002.png", "BOX_LABEL_003.png", "BOX_LABEL_004.png"])
            && ServerChecks.Header(response, HeaderNames.LabelCount) == "4");
        var boxes = zip.Entries.Select(entry => DecodeBarcode(entry)).ToArray();
        check("zip 里的顺序和 items 一致（并行渲染后按顺序打包）",
            boxes.SequenceEqual(["C2610080001", "C2610080002", "C2610080002", "C2610080003"]));

        var pdf = await Post(http, Batch("pdf", "zip", Item("C2610080001"), Item("C2610080002", 2)));
        using var pdfZip = new ZipArchive(new MemoryStream(await pdf.Content.ReadAsByteArrayAsync()));
        var pages = pdfZip.Entries.Select(entry =>
        {
            using var reader = new StreamReader(entry.Open(), Encoding.Latin1);
            return Regex.Count(reader.ReadToEnd(), @"/Type\s*/Page(?![s\w])");
        }).ToArray();
        check("zip PDF：每条一个文件，份数在文件里（第 2 个文件 2 页）", pages.SequenceEqual([1, 2]) && ServerChecks.Header(pdf, HeaderNames.LabelCount) == "3");
    }

    /// <summary>
    /// 错误：先校验全部条目、明细带序号，PNG 不能 merge，超过张数上限。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckErrorsAsync(HttpClient http, Action<string, bool> check)
    {
        var missing = await Error(await Post(http, Batch("pdf", "merge", Item("C2610080001"), """{"data":{}}""", """{"data":{"qty":"x"}}""")));
        check("先校验全部条目：任一条不通过整批返回，明细列出每条的问题并带序号",
            missing is { Code: ErrorCodes.FieldRequired, Errors.Count: >= 2 } && missing.Errors.Any(item => item.Index == 1) && missing.Errors.Any(item => item.Index == 2)
            && missing.Errors.All(item => item.Index is 1 or 2) && missing.Message.StartsWith("第 2 条", StringComparison.Ordinal));

        var badBarcode = await Error(await Post(http, Batch("pdf", "merge", Item("C2610080001"), Item("箱号二"))));
        check("merge 时条码内容不对：BARCODE_INVALID，明细带条目序号", badBarcode is { Code: ErrorCodes.BarcodeInvalid } && badBarcode.Errors?[0].Index == 1 && badBarcode.Message.StartsWith("第 2 条", StringComparison.Ordinal));
        var badZip = await Error(await Post(http, Batch("png", "zip", Item("C2610080001"), Item("C2610080002"), Item("箱号三"))));
        check("zip 时条码内容不对：BARCODE_INVALID，明细带条目序号", badZip is { Code: ErrorCodes.BarcodeInvalid } && badZip.Errors?[0].Index == 2);

        var tooMany = await Post(http, Batch("zpl", "merge", Enumerable.Range(0, 6).Select(_ => Item("C2610080001", 100)).ToArray()));
        check("总张数（含份数）超过 500 返回 413 BATCH_TOO_LARGE", (int)tooMany.StatusCode == 413 && (await Error(tooMany))?.Code == ErrorCodes.BatchTooLarge);
        check("output 非法返回 OUTPUT_INVALID", (await Error(await Post(http, Batch("pdf", "all", Item("C1")))))?.Code == ErrorCodes.OutputInvalid);
        check("PNG 用 merge 返回 OUTPUT_INVALID", (await Error(await Post(http, Batch("png", "merge", Item("C1")))))?.Code == ErrorCodes.OutputInvalid);
        var copies = await Error(await Post(http, Batch("pdf", "merge", Item("C1"), Item("C2", 0))));
        check("条目的 copies 超范围返回 COPIES_INVALID，明细带序号", copies is { Code: ErrorCodes.CopiesInvalid } && copies.Errors?[0].Index == 1);
        check("items 为空返回 INVALID_JSON", (await Error(await Post(http, """{"templateCode":"BOX_LABEL","format":"pdf","items":[]}""")))?.Code == ErrorCodes.InvalidJson);
    }

    /// <summary>
    /// 性能：100 张合并 PDF 在 3 秒内（先预热一次）。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckPerformanceAsync(HttpClient http, Action<string, bool> check)
    {
        var items = Enumerable.Range(1, 100).Select(i => Item($"C26100800{i:D2}")).ToArray();
        await Post(http, Batch("pdf", "merge", items));
        var started = Stopwatch.GetTimestamp();
        var response = await Post(http, Batch("pdf", "merge", items));
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Console.WriteLine($"INFO: 批量 100 张合并 PDF 用时 {elapsed:0} ms，{ServerChecks.Header(response, HeaderNames.ServerTiming)}");
        check("批量 100 张合并 PDF 在 3 秒内", response.IsSuccessStatusCode && ServerChecks.Header(response, HeaderNames.LabelCount) == "100" && elapsed < 3000);
    }

    /// <summary>
    /// 批量的一条：只给箱号，份数不是 1 时带上 copies。
    /// </summary>
    /// <param name="boxNo">箱号。</param>
    /// <param name="copies">份数。</param>
    /// <returns>条目 JSON。</returns>
    private static string Item(string boxNo, int copies = 1) =>
        JsonSerializer.Serialize(copies == 1 ? new
        {
            data = new
            {
                boxNo
            }
        } : (object)new
        {
            data = new
            {
                boxNo
            },
            copies
        });

    /// <summary>
    /// 批量请求体。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <param name="output">merge 或 zip；null 表示不传。</param>
    /// <param name="items">条目 JSON。</param>
    /// <returns>请求体 JSON。</returns>
    private static string Batch(string format, string? output, params string[] items)
    {
        var outputJson = output is null ? "" : $$""","output":"{{output}}" """.TrimEnd();
        return $$"""{"templateCode":"BOX_LABEL","format":"{{format}}"{{outputJson}},"common":{{Common}},"items":[{{string.Join(",", items)}}]}""";
    }

    /// <summary>
    /// 用开发 Key 调批量接口。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="json">请求体。</param>
    /// <returns>响应。</returns>
    private static async Task<HttpResponseMessage> Post(HttpClient http, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render/batch") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        return await http.SendAsync(request);
    }

    /// <summary>
    /// 读错误响应。
    /// </summary>
    /// <param name="response">响应。</param>
    /// <returns>错误响应；成功时为 null。</returns>
    private static async Task<ErrorResponse?> Error(HttpResponseMessage response) =>
        response.IsSuccessStatusCode ? null : await response.Content.ReadFromJsonAsync<ErrorResponse>(LabelJson.Options);

    /// <summary>
    /// 解码 ZIP 里一张 PNG 上的 Code128 条码。
    /// </summary>
    /// <param name="entry">ZIP 条目。</param>
    /// <returns>条码内容；扫不出来时为 null。</returns>
    private static string? DecodeBarcode(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        using var bitmap = SKBitmap.Decode(memory.ToArray());
        var gray = new byte[bitmap.Width * bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                gray[y * bitmap.Width + x] = bitmap.GetPixel(x, y).Red;
            }
        }

        var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.CODE_128], TryHarder = true } };
        return reader.Decode(new RGBLuminanceSource(gray, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8))?.Text;
    }
}
