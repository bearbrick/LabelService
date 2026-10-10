using BenchmarkDotNet.Running;

namespace LabelService.Benchmarks;

/// <summary>
/// 基准入口。<c>--gate</c>、<c>--update-baseline</c> 是基准门禁（见 <see cref="BaselineGate"/>），<c>--self-test</c> 检查门禁的覆盖规则（见 <see cref="GateCoverage"/>），其他参数交给 BenchmarkDotNet。CI 用法和防退化规则见 docs/performance.md。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            return GateCoverage.SelfTest();
        }

        if (args.Contains("--gate") || args.Contains("--update-baseline"))
        {
            return BaselineGate.Run(args);
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}
