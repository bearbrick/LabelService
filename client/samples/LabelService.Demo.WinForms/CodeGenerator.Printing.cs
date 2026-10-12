using System.Text;

namespace LabelService.Demo;

/// <summary>
/// 调用代码中的打印入口：三种格式共用 Windows 静默打印扩展，通常只配置打印机名称。
/// </summary>
internal static partial class CodeGenerator
{
    /// <summary>
    /// 打印和错误处理使用的命名空间。
    /// </summary>
    private static readonly string[] PrintUsings =
    [
        "System.ComponentModel", "System.Drawing.Printing", "System.Net.Sockets", "System.Runtime.InteropServices", "LabelService.Client.Printing",
    ];

    /// <summary>
    /// 文件头的引用说明；打印模块是可选 Windows 扩展，不修改 HTTP SDK 的依赖。
    /// </summary>
    /// <param name="code">生成中的源码。</param>
    private static void AppendHeader(StringBuilder code) => code.Append("""
        // 由 LabelServiceDemo 生成：引用 LabelService.Client 和 LabelService.Client.Windows，把此文件作为 Program.cs。
        // .NET Framework 项目引用 System.Drawing；.NET 8 以上使用 net8.0-windows 等 Windows 目标，并设置 <UseWindowsForms>true</UseWindowsForms>。
        // PNG、PDF、ZPL 共用静默打印入口；PDF 使用 Windows 自带渲染，需要 Windows 8.1+，不需要阅读器。


        """);

    /// <summary>
    /// 只填写 Windows 已安装的打印机名称，空字符串使用系统默认打印机。
    /// </summary>
    /// <param name="code">生成中的源码。</param>
    private static void AppendPrinterSettings(StringBuilder code) => code.Append("""
            // 实施人员已配置好驱动的纸张、方向等参数，这里只填 Windows 打印机名称；空字符串使用默认打印机。
            private const string PrinterName = "";


        """);

    /// <summary>
    /// 生成结果后直接提交静默打印，不先存文件，也不打开阅读器。
    /// </summary>
    /// <param name="code">生成中的源码。</param>
    private static void AppendPrintCall(StringBuilder code) => code.Append("""

                    // PNG、PDF、ZPL 和各自的 ZIP 都使用同一入口，直接打印内存里的结果。
                    // PNG、PDF 多页连续打印，保持实际尺寸；ZPL 打印机必须支持 ZPL，DPI 和份数在生成请求中设置。
                    WindowsLabelPrinter.Print(file, PrinterName);

                    // 需要留底时再另存一份：
                    // file.Save(Path.Combine(Path.GetTempPath(), file.FileName));

        """);

    /// <summary>
    /// 打印异常与服务调用异常分别处理，异常发生时不声称打印机已经出纸。
    /// </summary>
    /// <param name="code">生成中的源码。</param>
    private static void AppendPrintCatch(StringBuilder code) => code.Append("""
                catch (Exception ex) when (ex is InvalidPrinterException || ex is Win32Exception || ex is COMException
                    || ex is IOException || ex is InvalidOperationException || ex is ArgumentException
                    || ex is PlatformNotSupportedException || ex is TimeoutException || ex is SocketException)
                {
                    Console.WriteLine("打印提交失败：" + ex.Message);
                }

        """);

    /// <summary>
    /// 特殊参数的标准写法和网络 ZPL 用法作为注释放在末尾，普通客户无需展开底层方法。
    /// </summary>
    /// <param name="code">生成中的源码。</param>
    private static void AppendPrintMethods(StringBuilder code) => code.Append("""

            // 特殊情况（PNG、PDF）把上面的 Print 调用换成下面几行，先修改本次参数再打印，不改系统中的驱动配置：
            // using (var document = WindowsLabelPrinter.CreateDocument(file, PrinterName))
            // {
            //     document.PrinterSettings.Copies = 2;
            //     document.Print();
            // }
            // ZPL 使用原始指令，份数和 DPI 在生成请求中设置，不使用 CreateDocument。
            // 非 ZIP 的 ZPL 如需直连网络打印机，可替换为：
            // await NetworkPrinter.SendAsync("192.168.1.50", NetworkPrinter.DefaultPort, file.Content);

        """);
}
