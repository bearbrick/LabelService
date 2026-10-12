namespace LabelService.Client.Checks;

/// <summary>
/// 调用端检查程序入口：SDK 对假服务端的全部用例、协议解析、架构规则、注释规范；设置了 LABEL_SERVICE_URL 时再对真实服务跑端到端用例。
/// </summary>
/// <remarks>
/// 带参数 docs 时只检查注释，列出全部问题（不在第一处失败时停下），方便集中补注释。
/// </remarks>
internal static class Program
{
    /// <summary>
    /// 通过的检查项数。
    /// </summary>
    private static int passed;

    /// <summary>
    /// 只检查注释时失败的项数。
    /// </summary>
    private static int failed;

    /// <summary>
    /// 运行全部检查；任何一项失败时抛异常，进程以非零退出码结束。
    /// </summary>
    /// <param name="args">命令行参数；docs 表示只检查注释。</param>
    /// <returns>退出码：0 全部通过；只检查注释时有失败项为 1。</returns>
    private static async Task<int> Main(string[] args)
    {
        var clientRoot = ClientRoot();
        if (args.Contains("docs"))
        {
            DocumentationChecks.ListAll = true;
            DocumentationChecks.Run(clientRoot, Report);
            return failed == 0 ? 0 : 1;
        }

        await ClientChecks.RunAsync(Check);
        ProtocolChecks.Run(Check);
        JsonChecks.Run(Check);
        WindowsPrintChecks.Run(Check);
        ArchitectureChecks.Run(clientRoot, Check);
        DocumentationChecks.Run(clientRoot, Check);
        await LiveServerChecks.RunAsync(Check);
        Console.WriteLine($"调用端检查全部通过：{passed} 项");
        return 0;
    }

    /// <summary>
    /// 记录一项检查：通过时打印 PASS，失败时抛异常结束检查。
    /// </summary>
    /// <param name="name">检查项的中文描述。</param>
    /// <param name="value">是否通过。</param>
    /// <exception cref="Exception">检查没通过。</exception>
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
    /// 记录一项检查但不中断：只检查注释时用，失败的也打印出来接着检查。
    /// </summary>
    /// <param name="name">检查项的中文描述。</param>
    /// <param name="value">是否通过。</param>
    private static void Report(string name, bool value)
    {
        Console.WriteLine((value ? "PASS: " : "FAIL: ") + name);
        if (!value)
        {
            failed++;
        }
    }

    /// <summary>
    /// 从输出目录逐级向上找到调用端根目录（有 LabelService.Client.slnx 的目录）。
    /// </summary>
    /// <returns>client 目录的完整路径。</returns>
    /// <exception cref="DirectoryNotFoundException">不在仓库里运行。</exception>
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
