namespace LabelService.Backend.Checks;

/// <summary>
/// 后端检查程序入口。按“契约 → 纯函数 → 渲染 → 服务端 → 模板存储 → 架构与仓库规范 → 注释”的顺序执行，任何一项失败立即抛异常退出。
/// </summary>
/// <remarks>
/// 带参数 <c>docs</c> 时只检查注释（补注释时用，几秒钟就出结果）：<c>dotnet run --project backend/tests/LabelService.Backend.Checks -- docs</c>。
/// </remarks>
internal static class Program
{
    /// <summary>
    /// 已通过的检查项数。
    /// </summary>
    private static int passed;

    /// <summary>
    /// 运行全部检查。
    /// </summary>
    /// <param name="args">命令行参数；含 docs 时只检查注释。</param>
    /// <returns>全部通过时为 0；有失败时抛异常，进程以非零退出。</returns>
    private static async Task<int> Main(string[] args)
    {
        var backendRoot = Fixtures.BackendRoot();
        var repositoryRoot = Directory.GetParent(backendRoot)!.FullName;
        if (args.Contains("docs"))
        {
            // 只检查注释：列出全部问题，不在第一项失败时停下。
            var failed = 0;
            DocumentationChecks.ListAll = true;
            DocumentationChecks.Run(backendRoot, repositoryRoot, (name, value) =>
            {
                Console.WriteLine((value ? "PASS: " : "FAIL: ") + name);
                failed += value ? 0 : 1;
            });
            Console.WriteLine(failed == 0 ? "注释检查全部通过" : $"注释检查有 {failed} 项没通过");
            return failed == 0 ? 0 : 1;
        }

        ContractChecks.Run(backendRoot, repositoryRoot, Check);
        BindingChecks.Run(Check);
        ValidationChecks.Run(backendRoot, Check);
        RenderChecks.Run(backendRoot, Check);
        PngChecks.Run(backendRoot, Check);
        TextChecks.Run(backendRoot, Check);
        BarcodeChecks.Run(backendRoot, Check);
        QrCodeChecks.Run(backendRoot, Check);
        AssetChecks.Run(backendRoot, Check);
        PdfChecks.Run(backendRoot, Check);
        ZplChecks.Run(backendRoot, Check);
        LayoutRenderChecks.Run(backendRoot, Check);
        await ServerChecks.RunAsync(backendRoot, Check);
        await AutoValueChecks.RunAsync(backendRoot, Check);
        await DatabaseProviderChecks.RunAsync(backendRoot, Check);
        await StorageChecks.RunAsync(backendRoot, Check);
        await UpgradeChecks.RunAsync(backendRoot, Check);
        await BatchChecks.RunAsync(backendRoot, Check);
        await LayoutServerChecks.RunAsync(backendRoot, Check);
        await TimingChecks.RunAsync(backendRoot, Check);
        await ClientLogChecks.RunAsync(backendRoot, Check);
        await AdminChecks.RunAsync(backendRoot, Check);
        await FontUploadChecks.RunAsync(backendRoot, Check);
        await HostingChecks.RunAsync(backendRoot, Check);
        await MetricsChecks.RunAsync(backendRoot, Check);
        ArchitectureChecks.Run(backendRoot, repositoryRoot, Check);
        RepositorySourceChecks.Run(repositoryRoot, Check);
        DocumentationChecks.Run(backendRoot, repositoryRoot, Check);
        Console.WriteLine($"后端检查全部通过：{passed} 项");
        return 0;
    }

    /// <summary>
    /// 记录一项检查：通过时打印 PASS，失败时抛异常结束。
    /// </summary>
    /// <param name="name">检查的中文描述。</param>
    /// <param name="value">是否通过。</param>
    /// <exception cref="Exception">没通过。</exception>
    private static void Check(string name, bool value)
    {
        if (!value)
        {
            throw new Exception("FAIL: " + name);
        }

        Console.WriteLine("PASS: " + name);
        passed++;
    }
}
