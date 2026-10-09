namespace LabelService.Client.Checks;

/// <summary>
/// 调用端检查程序入口：SDK 对假服务端的全部用例、协议解析、架构规则；设置了 LABEL_SERVICE_URL 时再对真实服务跑端到端用例。
/// </summary>
internal static class Program
{
    private static int passed;

    private static async Task<int> Main()
    {
        var clientRoot = ClientRoot();
        await ClientChecks.RunAsync(Check);
        ProtocolChecks.Run(Check);
        ArchitectureChecks.Run(clientRoot, Check);
        await LiveServerChecks.RunAsync(Check);
        Console.WriteLine($"调用端检查全部通过：{passed} 项");
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

    /// <summary>
    /// 从输出目录逐级向上找到调用端根目录（有 LabelService.Client.slnx 的目录）。
    /// </summary>
    private static string ClientRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "LabelService.Client.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("未找到 LabelService.Client.slnx，请在仓库内运行检查程序。");
    }
}
