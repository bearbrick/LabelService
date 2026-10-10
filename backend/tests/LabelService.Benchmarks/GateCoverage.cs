namespace LabelService.Benchmarks;

/// <summary>
/// 基准门禁的覆盖检查（fail-closed）：一项基准没跑出结果、基线里的基准这次没出现、一项都没执行，都算不通过，不能只比较跑出来的那些。
/// </summary>
/// <remarks>
/// 背景：BenchmarkDotNet 自动构建失败时 executed benchmarks 为 0，旧门禁只比较有结果的项，空结果算“通过”。
/// </remarks>
internal static class GateCoverage
{
    /// <summary>
    /// 检查这次运行是否完整，返回全部问题；没有问题时返回空列表。
    /// </summary>
    /// <param name="discovered">发现的全部基准（名字和 <see cref="BaselineGate"/> 的写法一致：类名.方法名(参数)）。</param>
    /// <param name="results">跑出结果的基准和中位数（毫秒）。</param>
    /// <param name="baseline">基线；更新基线时为 null。</param>
    /// <param name="fullRun">没带 --filter（跑全部）：基线里的每一项都必须跑出来。</param>
    /// <param name="criticalErrors">BenchmarkDotNet 的严重验证错误（如构建失败）。</param>
    public static List<string> Problems(
        IReadOnlyCollection<string> discovered,
        IReadOnlyDictionary<string, double> results,
        IReadOnlyDictionary<string, double>? baseline,
        bool fullRun,
        IReadOnlyCollection<string> criticalErrors)
    {
        var problems = criticalErrors.Select(error => "BenchmarkDotNet 报错：" + error).ToList();
        if (discovered.Count == 0)
        {
            problems.Add("没有发现任何基准（--filter 写错了，或者程序集里没有基准）");
        }

        var missing = discovered.Where(name => !results.ContainsKey(name)).Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            problems.Add($"{missing.Count} 项基准没跑出结果（构建或运行失败）：{string.Join("、", missing)}");
        }

        if (baseline is not null)
        {
            // 跑全部时基线的每一项都要出现；带 --filter 时只要求本次发现的基准方法在基线里的各项都出现。
            var methods = discovered.Select(Method).ToHashSet(StringComparer.Ordinal);
            var absent = baseline.Keys
                .Where(name => (fullRun || methods.Contains(Method(name))) && !results.ContainsKey(name))
                .Order(StringComparer.Ordinal)
                .ToList();
            if (absent.Count > 0)
            {
                problems.Add($"基线里的 {absent.Count} 项这次没有结果：{string.Join("、", absent)}");
            }
        }

        if (results.Count == 0 && problems.Count == 0)
        {
            problems.Add("一项基准都没有执行");
        }

        return problems;
    }

    /// <summary>
    /// 基准名去掉参数部分：TemplateBenchmarks.Render(Format=Pdf, …) → TemplateBenchmarks.Render。
    /// </summary>
    public static string Method(string name)
    {
        var index = name.IndexOf('(', StringComparison.Ordinal);
        return index < 0 ? name : name[..index];
    }

    /// <summary>
    /// <c>--self-test</c>：用合成的运行结果检查上面的规则，不跑基准。返回退出码。
    /// </summary>
    public static int SelfTest()
    {
        var baseline = new Dictionary<string, double> { ["A.Run(X=1)"] = 1, ["A.Run(X=2)"] = 2, ["B.Run"] = 3 };
        var all = new[] { "A.Run(X=1)", "A.Run(X=2)", "B.Run" };
        var full = all.ToDictionary(name => name, _ => 1.0);
        var cases = new (string Name, bool Ok)[]
        {
            ("完整运行没有问题", Problems(all, full, baseline, true, []).Count == 0),
            ("构建失败、0 项执行时不通过", Problems(all, new Dictionary<string, double>(), baseline, true, ["build failed"]).Count > 0),
            ("什么都没发现时不通过", Problems([], new Dictionary<string, double>(), baseline, true, []).Count > 0),
            ("有一项没跑出结果时不通过", Problems(all, full.Where(item => item.Key != "B.Run").ToDictionary(), baseline, true, []).Count > 0),
            ("跑全部时基线的项缺席不通过", Problems(["A.Run(X=1)"], new Dictionary<string, double> { ["A.Run(X=1)"] = 1 }, baseline, true, []).Count > 0),
            ("带过滤时只要求同一方法的基线项", Problems(["B.Run"], new Dictionary<string, double> { ["B.Run"] = 1 }, baseline, false, []).Count == 0),
            ("带过滤时同一方法缺参数组合不通过", Problems(["A.Run(X=1)"], new Dictionary<string, double> { ["A.Run(X=1)"] = 1 }, baseline, false, []).Count > 0),
            ("更新基线时 0 项执行也不通过", Problems(all, new Dictionary<string, double>(), null, true, []).Count > 0),
        };
        foreach (var (name, ok) in cases)
        {
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        }

        return cases.All(item => item.Ok) ? 0 : 1;
    }
}
