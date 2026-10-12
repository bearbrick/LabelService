using System.Text;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 调用代码里“发送到打印机”的部分：按输出格式写出打印方法、要用的命名空间和打印出错时的处理。
/// </summary>
/// <remarks>
/// ZPL 直接发给标签打印机（网络 TCP 9100，或 USB 打印机按 RAW 写入打印队列）；PNG 用 Windows 打印机按标签原尺寸打印（批量是 ZIP，逐张打）；
/// PDF 交给本机的 PDF 阅读器打印。生成的代码只用 C# 7.3 的语法。
/// </remarks>
internal static partial class CodeGenerator
{
    /// <summary>
    /// 打印要用到的命名空间（在 System、System.IO、System.Threading.Tasks、LabelService.Client 之外）。
    /// </summary>
    /// <param name="settings">当前设置。</param>
    /// <returns>按字母顺序的命名空间。</returns>
    private static IEnumerable<string> PrintUsings(Settings settings) => settings.Format switch
    {
        LabelFormat.Zpl => ["System.ComponentModel", "System.Net.Sockets", "LabelService.Client.Printing"],
        LabelFormat.Png => settings.Batch
            ? ["System.ComponentModel", "System.Drawing", "System.Drawing.Printing", "System.IO.Compression"]
            : ["System.ComponentModel", "System.Drawing", "System.Drawing.Printing"],
        _ => ["System.ComponentModel", "System.Diagnostics"],
    };

    /// <summary>
    /// 文件开头的说明：怎么放进项目，PNG 打印在 .NET 8 项目里要怎么配。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    /// <param name="settings">当前设置。</param>
    private static void AppendHeader(StringBuilder code, Settings settings)
    {
        code.AppendLine("// 由 LabelServiceDemo 生成：新建控制台项目，引用 LabelService.Client 包，把这个文件作为 Program.cs。");
        if (settings.Format == LabelFormat.Png)
        {
            code.AppendLine("// PNG 打印用 System.Drawing：.NET Framework 自带；.NET 8 项目的 TargetFramework 写 net8.0-windows，并在项目文件里加 <UseWindowsForms>true</UseWindowsForms>。");
        }

        if (settings.Format == LabelFormat.Png && settings.Batch)
        {
            code.AppendLine("// 批量 PNG 是 ZIP：.NET Framework 项目还要引用 System.IO.Compression。");
        }

        code.AppendLine();
    }

    /// <summary>
    /// 类里的打印机设置常量。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    /// <param name="settings">当前设置。</param>
    private static void AppendPrinterSettings(StringBuilder code, Settings settings)
    {
        switch (settings.Format)
        {
            case LabelFormat.Zpl:
                code.AppendLine("    // 打印机设置：按实际情况改，也可以放配置文件。");
                code.AppendLine("    private const string PrinterHost = \"192.168.1.50\";              // 网络标签打印机的 IP（TCP 9100）");
                code.AppendLine("    private const string PrinterName = \"ZDesigner ZT410-300dpi ZPL\"; // USB 连接时用：控制面板里的打印机名称");
                break;
            case LabelFormat.Png:
                code.AppendLine("    // 打印机设置：控制面板里的打印机名称；空字符串表示 Windows 默认打印机。");
                code.AppendLine("    private const string PrinterName = \"\";");
                break;
            default:
                code.AppendLine("    // PDF 交给本机的 PDF 阅读器，用 Windows 默认打印机打印。");
                break;
        }

        code.AppendLine();
    }

    /// <summary>
    /// Main 里保存文件之后的打印调用。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    /// <param name="settings">当前设置。</param>
    private static void AppendPrintCall(StringBuilder code, Settings settings)
    {
        code.AppendLine();
        code.AppendLine("            // 发送到打印机（打印机地址、名称在上面的常量里改）");
        code.AppendLine(settings.Format switch
        {
            LabelFormat.Zpl => "            await PrintAsync(file);",
            LabelFormat.Png => "            Print(file);",
            _ => "            Print(path);",
        });
    }

    /// <summary>
    /// Main 里打印出错时的 catch：标签已经生成并保存了，打印机的问题处理好之后可以重打。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    /// <param name="settings">当前设置。</param>
    private static void AppendPrintCatch(StringBuilder code, Settings settings)
    {
        code.AppendLine(settings.Format switch
        {
            LabelFormat.Zpl => "        catch (Exception ex) when (ex is SocketException || ex is TimeoutException || ex is Win32Exception)",
            LabelFormat.Png => "        catch (Exception ex) when (ex is InvalidPrinterException || ex is Win32Exception)",
            _ => "        catch (Win32Exception ex)",
        });
        code.AppendLine("        {");
        code.AppendLine(settings.Format switch
        {
            LabelFormat.Zpl => "            // 打印机连不上、名称不对等：标签文件已经保存，处理好打印机后可以重发",
            LabelFormat.Png => "            // 打印机名称不对、打印机脱机等：标签文件已经保存，处理好后可以重打",
            _ => "            // 本机没有能打印 PDF 的程序（右键菜单里没有“打印”）：装一个 PDF 阅读器，或改用 ZPL 发给标签打印机",
        });
        code.AppendLine("            Console.WriteLine(\"打印失败：\" + ex.Message);");
        code.AppendLine("        }");
    }

    /// <summary>
    /// 类末尾的打印方法。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    /// <param name="settings">当前设置。</param>
    private static void AppendPrintMethods(StringBuilder code, Settings settings)
    {
        code.AppendLine();
        switch (settings.Format)
        {
            case LabelFormat.Zpl:
                code.Append("""
                        // ZPL：直接发给标签打印机，打印机自己按 ZPL 指令打（份数也在指令里）。
                        private static async Task PrintAsync(LabelFile file)
                        {
                            // 方式一：网络标签打印机（斑马等），TCP 9100。打印机收到数据就返回，不代表已经打完。
                            await NetworkPrinter.SendAsync(PrinterHost, NetworkPrinter.DefaultPort, file.Content);

                            // 方式二：USB 连接、装了驱动的标签打印机，按 RAW 写入 Windows 打印队列（用这种时把上面一行注释掉）。
                            // WindowsRawPrinter.Send(PrinterName, file.Content, file.FileName);
                        }

                    """);
                break;
            case LabelFormat.Png:
                code.Append(settings.Batch
                    ? """
                            // PNG 批量是 ZIP（一张一个 PNG），逐张打印。
                            private static void Print(LabelFile file)
                            {
                                using (var zip = new ZipArchive(file.OpenRead(), ZipArchiveMode.Read))
                                {
                                    foreach (var entry in zip.Entries)
                                    {
                                        using (var stream = entry.Open())
                                        using (var memory = new MemoryStream())
                                        {
                                            stream.CopyTo(memory);
                                            memory.Position = 0;
                                            using (var image = Image.FromStream(memory))
                                            {
                                                PrintImage(image, file.Dpi, entry.Name);
                                            }
                                        }
                                    }
                                }
                            }

                        """
                    : """
                            // PNG：用 Windows 打印机按标签原尺寸打印。
                            private static void Print(LabelFile file)
                            {
                                using (var image = Image.FromStream(new MemoryStream(file.Content)))
                                {
                                    PrintImage(image, file.Dpi, file.FileName);
                                }
                            }

                        """);
                code.Append("""

                        // 按标签的实际尺寸打印一张图：像素 ÷ DPI = 英寸，PrintDocument 的单位是 1/100 英寸；纸张设成标签大小，页边距为 0。
                        private static void PrintImage(Image image, int dpi, string documentName)
                        {
                            if (dpi <= 0)
                            {
                                dpi = 300;
                            }

                            var width = image.Width * 100 / dpi;
                            var height = image.Height * 100 / dpi;
                            using (var document = new PrintDocument())
                            {
                                document.DocumentName = documentName;
                                if (PrinterName.Length > 0)
                                {
                                    document.PrinterSettings.PrinterName = PrinterName;
                                }

                                document.DefaultPageSettings.PaperSize = new PaperSize("Label", width, height);
                                document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                                document.PrintPage += (sender, e) =>
                                {
                                    e.Graphics.DrawImage(image, new Rectangle(0, 0, width, height));
                                    e.HasMorePages = false;
                                };
                                document.Print();
                            }
                        }

                    """);
                break;
            default:
                code.Append("""
                        // PDF：标签打印机一般不能直接收 PDF，交给本机的 PDF 阅读器打印（要装支持“打印”命令的阅读器，如 Adobe Reader、福昕）。
                        // A4 拼版用激光打印机打时也可以这样；要静默、批量打标签，建议改用 ZPL 发给标签打印机。
                        private static void Print(string path)
                        {
                            Process.Start(new ProcessStartInfo(path) { Verb = "print", UseShellExecute = true });
                        }

                    """);
                break;
        }
    }
}
