namespace LabelService.Backend.Checks;

/// <summary>
/// 后端检查程序入口。按“契约 → 纯函数 → 渲染 → 服务端 → 模板存储 → 架构与仓库规范”的顺序执行，任何一项失败立即抛异常退出。
/// </summary>
internal static class Program
{
    private static int passed;

    private static async Task<int> Main()
    {
        var backendRoot = Fixtures.BackendRoot();
        var repositoryRoot = Directory.GetParent(backendRoot)!.FullName;
        ContractChecks.Run(backendRoot, repositoryRoot, Check);
        BindingChecks.Run(Check);
        ValidationChecks.Run(backendRoot, Check);
        RenderChecks.Run(backendRoot, Check);
        TextChecks.Run(backendRoot, Check);
        BarcodeChecks.Run(backendRoot, Check);
        QrCodeChecks.Run(backendRoot, Check);
        AssetChecks.Run(backendRoot, Check);
        PdfChecks.Run(backendRoot, Check);
        ZplChecks.Run(backendRoot, Check);
        LayoutRenderChecks.Run(backendRoot, Check);
        await ServerChecks.RunAsync(backendRoot, Check);
        await StorageChecks.RunAsync(backendRoot, Check);
        await UpgradeChecks.RunAsync(backendRoot, Check);
        await BatchChecks.RunAsync(backendRoot, Check);
        await LayoutServerChecks.RunAsync(backendRoot, Check);
        await TimingChecks.RunAsync(backendRoot, Check);
        await ClientLogChecks.RunAsync(backendRoot, Check);
        await AdminChecks.RunAsync(backendRoot, Check);
        await HostingChecks.RunAsync(backendRoot, Check);
        await MetricsChecks.RunAsync(backendRoot, Check);
        ArchitectureChecks.Run(backendRoot, repositoryRoot, Check);
        Console.WriteLine($"后端检查全部通过：{passed} 项");
        return 0;
    }

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
