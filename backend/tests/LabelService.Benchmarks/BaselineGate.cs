using System.Globalization;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace LabelService.Benchmarks;

/// <summary>
/// 基准门禁（设计书第 9 节“压测与防退化”）：跑全部基准，中位数比基线慢 20% 以上就返回非零退出码，CI 据此拦住合并。
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

    /// <summary>
    /// 读写基线文件的 JSON 设置：缩进、中文不转义（方便人看和比较差异）。
    /// </summary>
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 运行门禁或更新基线，返回退出码。
    /// </summary>
    /// <param name="args">命令行参数（--gate、--update-baseline、--filter、--baseline）。</param>
    /// <returns>退出码：0 通过，1 有基准变慢或运行不完整。</returns>
    public static int Run(string[] args)
    {
        var path = Option(args, "--baseline") ?? BenchmarkPaths.BaselineFile();
        var filter = Option(args, "--filter") ?? "*";
        var config = DefaultConfig.Instance
            .AddJob(Job.ShortRun.WithIterationCount(10).WithId("gate"))
            .AddFilter(new GlobFilter([filter]))
            .WithOptions(ConfigOptions.JoinSummary);
        var update = args.Contains("--update-baseline");
        if (!update && !File.Exists(path))
        {
            Console.Error.WriteLine($"没有基线文件 {path}，先用 --update-baseline 生成");
            return 2;
        }

        var expected = update ? null : JsonSerializer.Deserialize<Baseline>(File.ReadAllText(path))!;
        // 运行前先按同样的过滤条件列出应该跑的基准：构建失败时 BenchmarkDotNet 的结果里连基准列表都没有，不能拿它当“应该跑哪些”。
        var discovered = typeof(BaselineGate).Assembly.GetTypes()
            .Where(type => type.IsPublic && !type.IsAbstract && type.GetMethods().Any(method => method.IsDefined(typeof(BenchmarkAttribute), false)))
            .SelectMany(type => BenchmarkConverter.TypeToBenchmarks(type, config).BenchmarksCases)
            .Select(Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var summaries = BenchmarkRunner.Run(typeof(BaselineGate).Assembly, config);
        var reports = summaries.SelectMany(summary => summary.Reports).ToList();
        var results = reports
            .Where(report => report.Success && report.ResultStatistics is not null)
            .ToDictionary(report => Name(report), report => Math.Round(report.ResultStatistics!.Median / 1e6, 3), StringComparer.Ordinal);
        var critical = summaries.SelectMany(summary => summary.ValidationErrors).Where(error => error.IsCritical).Select(error => error.Message)
            .Concat(reports.Where(report => !report.BuildResult.IsBuildSuccess).Select(report => "构建失败 " + Name(report) + "：" + report.BuildResult.ErrorMessage))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var machine = Machine(summaries);

        // 覆盖不完整一律不通过，也不写基线（fail-closed）。
        var problems = GateCoverage.Problems(discovered, results, expected?.Results, filter == "*", critical);
        Console.WriteLine($"发现 {discovered.Count} 项基准，跑出结果 {results.Count} 项");
        if (problems.Count > 0)
        {
            foreach (var problem in problems)
            {
                Console.Error.WriteLine("门禁不通过：" + problem);
            }

            Console.WriteLine(update ? "基线没有更新：这次运行不完整" : "基准门禁不通过：运行不完整");
            return 3;
        }

        foreach (var report in reports.Where(report => report.ResultStatistics is not null).OrderBy(Name, StringComparer.Ordinal))
        {
            // 门禁按中位数比较；P95 只打印出来供记录，不能拿中位数当 P95。
            var stats = report.ResultStatistics!;
            Console.WriteLine($"统计 {Name(report)}：中位数 {stats.Median / 1e6:0.000} ms，P95 {stats.Percentiles.P95 / 1e6:0.000} ms，样本 {stats.N}");
        }

        if (update)
        {
            var baseline = new Baseline(machine, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), results.OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary());
            File.WriteAllText(path, JsonSerializer.Serialize(baseline, Json) + Environment.NewLine);
            Console.WriteLine($"已写入基线 {path}（{results.Count} 项，{machine}）");
            return 0;
        }

        if (expected!.Machine != machine)
        {
            Console.WriteLine($"注意：基线是在“{expected.Machine}”上测的，这台是“{machine}”，比较只在同规格机器上有意义。");
        }

        var failed = 0;
        var added = 0;
        Console.WriteLine($"{"基准",-70} {"基线 ms",10} {"本次 ms",10} {"变化",8}");
        foreach (var (name, median) in results.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!expected.Results.TryGetValue(name, out var before))
            {
                added++;
                Console.WriteLine($"{name,-70} {"—",10} {median,10:0.000} {"新增",8}");
                continue;
            }

            var change = median / before - 1;
            var slow = change > Threshold;
            failed += slow ? 1 : 0;
            Console.WriteLine($"{name,-70} {before,10:0.000} {median,10:0.000} {change,8:+0%;-0%;0%}{(slow ? "  ← 变慢超过 20%" : "")}");
        }

        if (added > 0)
        {
            Console.WriteLine($"另有 {added} 项基线里没有（新增基准），只打印不比较；要纳入门禁须在同规格机器上补基线");
        }

        Console.WriteLine(failed == 0 ? $"基准门禁通过：{results.Count - added} 项和基线比较" : $"基准门禁不通过：{failed} 项比基线慢 20% 以上");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// 基准名：类名.方法名(参数)，如 TemplateBenchmarks.Render(Format=Pdf, Template=a4-shipping-mark)。
    /// </summary>
    /// <param name="report">一项基准的结果。</param>
    /// <returns>基准名。</returns>
    private static string Name(BenchmarkReport report) => Name(report.BenchmarkCase);

    /// <summary>
    /// 基准名：类名.方法名(参数)，参数部分去掉 BenchmarkDotNet 加的方括号。
    /// </summary>
    /// <param name="benchmark">一项基准。</param>
    /// <returns>基准名。</returns>
    private static string Name(BenchmarkCase benchmark)
    {
        var parameters = benchmark.Parameters.DisplayInfo;
        return $"{benchmark.Descriptor.Type.Name}.{benchmark.Descriptor.WorkloadMethod.Name}{(string.IsNullOrEmpty(parameters) ? "" : "(" + parameters.Trim('[', ']') + ")")}";
    }

    /// <summary>
    /// 这次运行的机器信息：CPU 型号、逻辑核数、运行时版本，写进基线、比较时提示是否一致。
    /// </summary>
    /// <param name="summaries">各基准类的运行汇总。</param>
    /// <returns>机器信息。</returns>
    private static string Machine(Summary[] summaries)
    {
        var host = summaries.FirstOrDefault()?.HostEnvironmentInfo;
        var cpu = host?.Cpu.Value?.ProcessorName ?? "未知 CPU";
        return $"{cpu.Trim()}，{Environment.ProcessorCount} 个逻辑核，{host?.RuntimeVersion}";
    }

    /// <summary>
    /// 取命令行里某个选项后面的值。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="name">选项名，如 --filter。</param>
    /// <returns>选项的值；没有这个选项或后面没有值时为 null。</returns>
    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>
    /// 基线文件。
    /// </summary>
    /// <param name="Machine">测基线的机器。</param>
    /// <param name="Updated">更新日期。</param>
    /// <param name="Results">各基准的中位数，毫秒。</param>
    private sealed record Baseline(string Machine, string Updated, Dictionary<string, double> Results);
}
