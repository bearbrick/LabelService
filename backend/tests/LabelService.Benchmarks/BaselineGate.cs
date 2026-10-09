using System.Globalization;
using System.Text.Json;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace LabelService.Benchmarks;

/// <summary>
/// 基准门禁（T-015、设计书第 9 节“压测与防退化”）：跑全部基准，中位数比基线慢 20% 以上就返回非零退出码，CI 据此拦住合并。
/// </summary>
/// <remarks>
/// <para>
/// <c>--gate</c>：比较；<c>--update-baseline</c>：把这次的结果写成新基线（baseline.json）。可以加 <c>--filter 通配符</c> 只跑一部分，
/// <c>--baseline 路径</c> 换基线文件。用 ShortRun 加到 10 次迭代（3 次时同一台机器两次运行能差 25%），全部跑完约 5 分钟。
/// </para>
/// <para>
/// 基线和机器有关：必须在跑门禁的那台（或同规格的）机器上生成。基线里记了机器信息，不一致时会提示。
/// </para>
/// </remarks>
internal static class BaselineGate
{
    /// <summary>
    /// 允许的变慢比例。
    /// </summary>
    public const double Threshold = 0.20;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 运行门禁或更新基线，返回退出码。
    /// </summary>
    public static int Run(string[] args)
    {
        var path = Option(args, "--baseline") ?? BenchmarkPaths.BaselineFile();
        var filter = Option(args, "--filter") ?? "*";
        var config = DefaultConfig.Instance
            .AddJob(Job.ShortRun.WithIterationCount(10).WithId("gate"))
            .AddFilter(new GlobFilter([filter]))
            .WithOptions(ConfigOptions.JoinSummary);
        var summaries = BenchmarkRunner.Run(typeof(BaselineGate).Assembly, config);
        var results = summaries.SelectMany(summary => summary.Reports)
            .Where(report => report.ResultStatistics is not null)
            .ToDictionary(report => Name(report), report => Math.Round(report.ResultStatistics!.Median / 1e6, 3), StringComparer.Ordinal);
        var machine = Machine(summaries);
        if (args.Contains("--update-baseline"))
        {
            var baseline = new Baseline(machine, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), results.OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary());
            File.WriteAllText(path, JsonSerializer.Serialize(baseline, Json) + Environment.NewLine);
            Console.WriteLine($"已写入基线 {path}（{results.Count} 项，{machine}）");
            return 0;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"没有基线文件 {path}，先用 --update-baseline 生成");
            return 2;
        }

        var expected = JsonSerializer.Deserialize<Baseline>(File.ReadAllText(path))!;
        if (expected.Machine != machine)
        {
            Console.WriteLine($"注意：基线是在“{expected.Machine}”上测的，这台是“{machine}”，比较只在同规格机器上有意义。");
        }

        var failed = 0;
        Console.WriteLine($"{"基准",-70} {"基线 ms",10} {"本次 ms",10} {"变化",8}");
        foreach (var (name, median) in results.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!expected.Results.TryGetValue(name, out var before))
            {
                Console.WriteLine($"{name,-70} {"—",10} {median,10:0.000} {"新增",8}");
                continue;
            }

            var change = median / before - 1;
            var slow = change > Threshold;
            failed += slow ? 1 : 0;
            Console.WriteLine($"{name,-70} {before,10:0.000} {median,10:0.000} {change,8:+0%;-0%;0%}{(slow ? "  ← 变慢超过 20%" : "")}");
        }

        Console.WriteLine(failed == 0 ? "基准门禁通过" : $"基准门禁不通过：{failed} 项比基线慢 20% 以上");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// 基准名：类名.方法名(参数)，如 TemplateBenchmarks.Render(Format=Pdf, Template=a4-shipping-mark)。
    /// </summary>
    private static string Name(BenchmarkReport report)
    {
        var benchmark = report.BenchmarkCase;
        var parameters = benchmark.Parameters.DisplayInfo;
        return $"{benchmark.Descriptor.Type.Name}.{benchmark.Descriptor.WorkloadMethod.Name}{(string.IsNullOrEmpty(parameters) ? "" : "(" + parameters.Trim('[', ']') + ")")}";
    }

    private static string Machine(Summary[] summaries)
    {
        var host = summaries.FirstOrDefault()?.HostEnvironmentInfo;
        var cpu = host?.Cpu.Value?.ProcessorName ?? "未知 CPU";
        return $"{cpu.Trim()}，{Environment.ProcessorCount} 个逻辑核，{host?.RuntimeVersion}";
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>基线文件。</summary>
    /// <param name="Machine">测基线的机器。</param>
    /// <param name="Updated">更新日期。</param>
    /// <param name="Results">各基准的中位数，毫秒。</param>
    private sealed record Baseline(string Machine, string Updated, Dictionary<string, double> Results);
}
