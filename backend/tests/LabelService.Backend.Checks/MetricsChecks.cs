using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LabelService.Api.Diagnostics;
using LabelService.Application.Diagnostics;
using LabelService.Contracts.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace LabelService.Backend.Checks;

/// <summary>
/// 监控：/metrics 的 Prometheus 文本格式、指标名和设计书一致、维度里没有高基数字段、缓存命中率、运行时和 ASP.NET Core 指标。
/// </summary>
internal static class MetricsChecks
{
    /// <summary>
    /// 设计书第 9 节列的指标名。
    /// </summary>
    private static readonly string[] DesignMetrics =
    [
        "label_render_duration_ms", "label_render_stage_ms", "label_requests_total", "labels_generated_total",
        "label_inflight_requests", "label_queue_wait_ms", "label_cache_hit_ratio",
    ];

    /// <summary>
    /// 运行监控的全部检查：用临时数据目录启动服务，调几次接口后读 /metrics。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        CheckFormat(check);
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory);
            using var http = factory.CreateClient();
            await http.GetAsync("/health/ready");
            (await Render(http, "BOX_LABEL")).Dispose();
            (await Render(http, "NO_SUCH_TEMPLATE")).Dispose();
            var metrics = await http.GetAsync("/metrics");
            var text = await metrics.Content.ReadAsStringAsync();
            var names = Regex.Matches(text, "^# TYPE (\\S+) ", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToHashSet();
            var missing = DesignMetrics.Where(name => !names.Contains(name)).ToArray();
            check("/metrics 是 Prometheus 文本格式，指标名和设计书第 9 节一致" + (missing.Length > 0 ? "，缺：" + string.Join("、", missing) : ""),
                metrics.StatusCode == HttpStatusCode.OK && metrics.Content.Headers.ContentType?.MediaType == "text/plain" && missing.Length == 0);
            check("请求量按接口、调用方、结果码分开，失败的调用记错误码",
                text.Contains("label_requests_total{client=\"local-dev\",endpoint=\"render\",result=\"ok\"} 1", StringComparison.Ordinal)
                && text.Contains("label_requests_total{client=\"local-dev\",endpoint=\"render\",result=\"TEMPLATE_NOT_FOUND\"} 1", StringComparison.Ordinal));
            check("耗时直方图有分桶，P95 等可以用 histogram_quantile 算",
                Regex.IsMatch(text, "label_render_duration_ms_bucket\\{[^}]*endpoint=\"render\"[^}]*le=\"100\"\\} \\d") && text.Contains("label_render_stage_ms_count{", StringComparison.Ordinal));
            check("缓存命中率按模板、字体、图片、条码分开",
                text.Contains("label_cache_hit_ratio{cache=\"templates\"}", StringComparison.Ordinal) && text.Contains("label_cache_hit_ratio{cache=\"symbols\"}", StringComparison.Ordinal)
                && text.Contains("label_cache_hit_ratio{cache=\"fonts\"}", StringComparison.Ordinal));
            check("带进程指标（CPU、内存、GC、线程池）和 ASP.NET Core 的请求耗时",
                text.Contains("dotnet_process_cpu_time_seconds_total", StringComparison.Ordinal) && text.Contains("dotnet_gc_collections_total", StringComparison.Ordinal)
                && text.Contains("dotnet_thread_pool_queue_length", StringComparison.Ordinal) && text.Contains("http_server_request_duration_seconds_bucket", StringComparison.Ordinal));
            var monitoring = Path.Combine(backendRoot, "deploy", "monitoring");
            var referenced = new[] { "grafana-dashboard.json", "alerts.yml" }
                .SelectMany(file => Regex.Matches(File.ReadAllText(Path.Combine(monitoring, file)), "\\b(?:labels?_|dotnet_|http_server_)[a-z_]+").Select(match => match.Value))
                .ToHashSet();
            var unknown = referenced.Where(name => !names.Contains(name) && !names.Contains(Regex.Replace(name, "_(bucket|sum|count)$", ""))).Order().ToArray();
            check("仪表盘和告警规则里用到的指标 /metrics 里都有（" + referenced.Count + " 个）" + (unknown.Length > 0 ? "，没有：" + string.Join("、", unknown) : ""),
                referenced.Count > 10 && unknown.Length == 0);
            var labels = Regex.Matches(text, "([a-z_]+)=\"", RegexOptions.None).Select(match => match.Groups[1].Value).ToHashSet();
            check("指标维度里没有 requestId、traceId、数据值这类高基数字段：" + string.Join("、", labels.Order()),
                !labels.Any(label => label.Contains("request_id", StringComparison.Ordinal) || label.Contains("trace", StringComparison.Ordinal) || label is "data" or "ip" or "client_address"));
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    /// <summary>
    /// 导出格式本身：直方图累计分桶、sum 和 count、标签值转义，其他 Meter 的指标名转换。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    private static void CheckFormat(Action<string, bool> check)
    {

        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meters = provider.GetRequiredService<IMeterFactory>();
        using var exporter = new PrometheusExporter(meters);
        var meter = meters.Create(LabelMetrics.MeterName);
        var histogram = meter.CreateHistogram<double>("check_duration_ms");
        histogram.Record(5, new KeyValuePair<string, object?>("format", "png"));
        histogram.Record(100, new KeyValuePair<string, object?>("format", "png"));
        histogram.Record(3000, new KeyValuePair<string, object?>("format", "png"));
        meter.CreateCounter<long>("check_total").Add(2, new KeyValuePair<string, object?>("note", "a\"b"));
        using var other = new Meter(LabelMetrics.MeterName);
        other.CreateCounter<long>("check_other_host_total").Add(1);
        var text = exporter.Scrape();
        check("直方图按 le 累计分桶（含 +Inf）、带 sum 和 count；标签值转义；别的主机的同名 Meter 不混进来",
            text.Contains("check_duration_ms_bucket{format=\"png\",le=\"5\"} 1\n", StringComparison.Ordinal)
            && text.Contains("check_duration_ms_bucket{format=\"png\",le=\"100\"} 2\n", StringComparison.Ordinal)
            && text.Contains("check_duration_ms_bucket{format=\"png\",le=\"+Inf\"} 3\n", StringComparison.Ordinal)
            && text.Contains("check_duration_ms_sum{format=\"png\"} 3105\n", StringComparison.Ordinal)
            && text.Contains("check_total{note=\"a\\\"b\"} 2\n", StringComparison.Ordinal)
            && !text.Contains("check_other_host_total", StringComparison.Ordinal));

        using var runtime = new Meter("Some.Library");
        var seconds = runtime.CreateCounter<double>("http.client.wait", unit: "s");
        var bytes = runtime.CreateUpDownCounter<long>("process.memory", unit: "By");
        check("其他 Meter 的名字按 Prometheus 习惯转换：点换下划线，加单位后缀，计数器加 _total",
            PrometheusExporter.MetricName(seconds, PrometheusExporter.Kind.Counter) == "http_client_wait_seconds_total"
            && PrometheusExporter.MetricName(bytes, PrometheusExporter.Kind.Gauge) == "process_memory_bytes");
    }

    /// <summary>
    /// 用示例请求体和开发 Key 调一次单张生成，模板编码换成指定的。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="template">模板编码（不存在的编码用来造失败的调用）。</param>
    /// <returns>响应。</returns>
    private static Task<HttpResponseMessage> Render(HttpClient http, string template)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render")
        {
            Content = new StringContent(
                File.ReadAllText(Path.Combine(Fixtures.BackendRoot(), "samples", "requests", "box-label.render.json")).Replace("BOX_LABEL", template, StringComparison.Ordinal),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        return http.SendAsync(request);
    }
}
