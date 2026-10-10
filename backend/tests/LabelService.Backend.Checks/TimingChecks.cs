using System.Text;
using System.Text.RegularExpressions;
using LabelService.Contracts.Protocol;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using LabelService.Server.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LabelService.Backend.Checks;

/// <summary>
/// 计时口径（docs/performance.md：从开始处理请求算到写完最后一个字节）：调用日志和指标的总耗时要包含写响应体的时间。
/// Server-Timing 头先于响应体发出，它的 total 只能到写响应头为止。
/// </summary>
/// <remarks>
/// 做法：客户端拿到响应头后先停一会儿再读响应体。响应远大于服务器到客户端的管道缓冲，服务端写响应体时要等客户端读，
/// 所以“写完最后一个字节”一定在客户端开始读之后。
/// </remarks>
internal static partial class TimingChecks
{
    private const int ReaderDelayMs = 800;

    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory);
            using var http = factory.CreateClient();
            var data = File.ReadAllText(Path.Combine(backendRoot, "samples", "requests", "box-label.render.json"));
            var common = System.Text.Json.JsonDocument.Parse(data).RootElement.GetProperty("data").GetRawText();
            var items = string.Join(",", Enumerable.Range(1, 20).Select(i => "{\"data\":{\"boxNo\":\"C26100800" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "\"}}"));
            var body = "{\"templateCode\":\"BOX_LABEL\",\"format\":\"png\",\"output\":\"zip\",\"common\":" + common + ",\"items\":[" + items + "]}";

            var (fastHeader, fastBytes) = await SendAsync(http, body, "TIMING-FAST", delayMs: 0);
            var (slowHeader, slowBytes) = await SendAsync(http, body, "TIMING-SLOW", delayMs: ReaderDelayMs);
            var logs = factory.Services.GetRequiredService<RenderLogQuery>();
            var fast = await WaitForLogAsync(logs, "TIMING-FAST");
            var slow = await WaitForLogAsync(logs, "TIMING-SLOW");
            Console.WriteLine($"INFO: 计时口径：响应 {slowBytes / 1024} KB；正常读取 头 total {fastHeader:0.0} ms / 日志 {fast?.DurationMs:0.0} ms；停 {ReaderDelayMs} ms 再读 头 total {slowHeader:0.0} ms / 日志 {slow?.DurationMs:0.0} ms");
            check($"调用日志的总耗时算到写完最后一个字节：客户端停 {ReaderDelayMs} ms 才读约 1 MB 的响应体时，日志耗时至少 {ReaderDelayMs} ms",
                slowBytes > 256 * 1024 && slow is { Status: "ok" } && slow.DurationMs >= ReaderDelayMs);
            check("Server-Timing 头的 total 在写响应体之前就确定了，不含客户端读响应体的等待",
                slowHeader < ReaderDelayMs && slowHeader <= slow!.DurationMs);
            check("正常读取时日志耗时和 Server-Timing 的 total 同一量级（只差写响应体的时间），说明口径一致",
                fast is { Status: "ok" } && fast.DurationMs >= fastHeader && fast.DurationMs < ReaderDelayMs && fastBytes == slowBytes);
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    private static async Task<(double HeaderTotal, long Bytes)> SendAsync(HttpClient http, string json, string requestId, int delayMs)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render/batch") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        request.Headers.Add(HeaderNames.RequestId, requestId);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var timing = ServerChecks.Header(response, HeaderNames.ServerTiming) ?? "";
        var match = TotalPattern().Match(timing);
        var headerTotal = match.Success ? double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : double.NaN;
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }

        var bytes = (await response.Content.ReadAsByteArrayAsync()).LongLength;
        return (headerTotal, bytes);
    }

    private static async Task<RenderLogEntity?> WaitForLogAsync(RenderLogQuery logs, string requestId)
    {
        for (var i = 0; i < 100; i++)
        {
            var found = (await logs.SearchAsync(new RenderLogFilter(PageSize: 10, Search: requestId))).FirstOrDefault(log => log.RequestId == requestId);
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50);
        }

        return null;
    }

    [GeneratedRegex(@"total;dur=([\d.]+)")]
    private static partial Regex TotalPattern();
}
