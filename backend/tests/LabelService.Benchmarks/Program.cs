using BenchmarkDotNet.Running;

namespace LabelService.Benchmarks;

/// <summary>
/// 基准入口。CI 用法和防退化规则见 docs/performance.md。
/// </summary>
internal static class Program
{
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
