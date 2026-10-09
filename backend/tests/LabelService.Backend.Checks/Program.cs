namespace LabelService.Backend.Checks;

/// <summary>
/// 后端检查程序入口。按“契约 → 纯函数 → 渲染 → 服务端 → 架构与仓库规范”的顺序执行，任何一项失败立即抛异常退出。
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
        await ServerChecks.RunAsync(backendRoot, Check);
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
