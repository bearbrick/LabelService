using System.Text;

namespace LabelService.Demo;

/// <summary>
/// 调用代码里“发送到打印机”的部分：不管左边选的是哪种格式，三种格式的打印写法都写全，按生成结果的格式自动选。
/// </summary>
/// <remarks>
/// ZPL 直接发给标签打印机（网络 TCP 9100，或 USB 打印机按 RAW 写入打印队列），多张都在一个文件里；PNG 用 PrintDocument 按“像素 ÷ DPI”画成实际尺寸，
/// 多张（ZIP）放在同一个打印任务里一张一页；PDF 发给支持 PDF 直接打印的打印机，或交给本机的 PDF 阅读器，多页整份打。
/// 打印机的地址、名称都是类里的常量，改常量就能换打法，不用改代码。生成的代码只用 C# 7.3 的语法。
/// </remarks>
internal static partial class CodeGenerator
{
    /// <summary>
    /// 打印要用到的命名空间（在 System、System.IO、System.Threading.Tasks、LabelService.Client 之外）。
    /// </summary>
    private static readonly string[] PrintUsings =
    [
        "System.Collections.Generic", "System.ComponentModel", "System.Diagnostics", "System.Drawing", "System.Drawing.Printing",
        "System.IO.Compression", "System.Net.Sockets", "LabelService.Client.Printing",
    ];

    /// <summary>
    /// 文件开头的说明：怎么放进项目，打印 PNG、解 ZIP 要加的引用。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    private static void AppendHeader(StringBuilder code) => code.Append("""
        // 由 LabelServiceDemo 生成：新建控制台项目，引用 LabelService.Client 包，把这个文件作为 Program.cs。
        // 打印 PNG 用到 System.Drawing，解开 ZIP 用到 System.IO.Compression：
        //   .NET Framework 项目引用 System.Drawing、System.IO.Compression 两个程序集；
        //   .NET 8 项目的 TargetFramework 写 net8.0-windows，并在项目文件里加 <UseWindowsForms>true</UseWindowsForms>。
        // 只打 ZPL 或 PDF、不想引用 System.Drawing 时，删掉 PrintPng 和用到它的地方即可。


        """);

    /// <summary>
    /// 类里的打印机设置常量：ZPL 标签打印机、PNG 和 PDF 用的 Windows 打印机、支持 PDF 直接打印的打印机。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    private static void AppendPrinterSettings(StringBuilder code) => code.Append("""
            // 打印机设置：按实际情况改，也可以放配置文件。
            // ZPL 标签打印机（斑马等）：网络打印机填 IP；USB 连接的填控制面板里的打印机名称，填了名称就不用 IP。
            private const string ZplPrinterHost = "192.168.1.50";
            private const string ZplPrinterName = "";        // 如 "ZDesigner ZT410-300dpi ZPL"
            // PNG、PDF 用的 Windows 打印机：控制面板里的打印机名称；空字符串表示 Windows 默认打印机。
            private const string PrinterName = "";
            // 打印机支持 PDF 直接打印时填它的名称：PDF 直接发过去，不经过 PDF 阅读器；空字符串表示交给 PDF 阅读器打印。
            private const string PdfDirectPrinterName = "";


        """);

    /// <summary>
    /// Main 里生成之后的打印调用：直接用内存里的结果打印，不用先存文件；另存一份留底写成注释，需要时再打开。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    private static void AppendPrintCall(StringBuilder code) => code.Append("""

                    // 发送到打印机：直接用内存里的生成结果，不用先存文件；按结果的格式自动选打法（打印机在上面的常量里改）
                    await PrintAsync(file);

                    // 需要留底（出问题时好查、好重打）时可以另存一份：
                    // file.Save(Path.Combine(Path.GetTempPath(), file.FileName));

        """);

    /// <summary>
    /// Main 里打印出错时的 catch：标签已经生成了，打印机的问题处理好之后可以重打。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    private static void AppendPrintCatch(StringBuilder code) => code.Append("""
                catch (Exception ex) when (ex is SocketException || ex is TimeoutException || ex is Win32Exception || ex is InvalidPrinterException)
                {
                    // 打印机连不上、名称不对、脱机，或本机没有能打印 PDF 的阅读器：标签已经生成好了，处理好打印机后再打一次就行
                    Console.WriteLine("打印失败：" + ex.Message);
                }

        """);

    /// <summary>
    /// 类末尾的打印方法：按格式分派、ZPL、PNG（多张一个打印任务）、PDF（直接打印或交给阅读器）、解 ZIP。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    private static void AppendPrintMethods(StringBuilder code) => code.Append("""

            // ======== 打印 ========
            // 三种格式的打法都在下面，PrintAsync 按生成结果的格式自动选；只用一种格式时，其余的方法可以删掉。
            // 一次生成多张（份数、批量、A4 拼版多页）时怎么打：
            //   ZPL：多张都在一个文件里（每张一段 ^XA…^XZ，份数是 ^PQ），整个发一次，打印机按顺序全部打出来；
            //   PDF：多张是一个多页的 PDF，整份发给打印机或交给阅读器，一次打完所有页；
            //   PNG：一个 PNG 只能放一张，多张是 ZIP（一张一个 PNG），解开后放进同一个打印任务，一张一页。
            // 批量选了 Output = BatchOutput.Zip 时 ZPL、PDF 也是 ZIP（一条一个文件），同样解开后逐个发。
            private static async Task PrintAsync(LabelFile file)
            {
                var files = Files(file);
                switch (file.Format)
                {
                    case LabelFormat.Zpl:
                        foreach (var zpl in files)
                        {
                            await PrintZplAsync(zpl.Key, zpl.Value);
                        }

                        break;
                    case LabelFormat.Png:
                        PrintPng(file.FileName, files, file.Dpi);
                        break;
                    default:
                        foreach (var pdf in files)
                        {
                            PrintPdf(pdf.Key, pdf.Value);
                        }

                        break;
                }
            }

            // ZPL：标签打印机（斑马等）自己的指令，内容直接发给打印机就打，不经过驱动排版，也不会缩放。
            // 生成时 DPI 要和打印机一致（203、300 或 600，看型号或打印机自检页），否则打出来大小不对：请求里写 Dpi = 打印机的 DPI。
            // 想看指令内容可以用 file.GetZplText()。
            private static async Task PrintZplAsync(string name, byte[] zpl)
            {
                if (ZplPrinterName.Length > 0)
                {
                    // USB 连接、装了驱动的标签打印机：按 RAW 写入 Windows 打印队列，驱动原样转给打印机
                    WindowsRawPrinter.Send(ZplPrinterName, zpl, name);
                }
                else
                {
                    // 网络标签打印机：TCP 9100。打印机收到数据就返回，不代表已经打完
                    await NetworkPrinter.SendAsync(ZplPrinterHost, NetworkPrinter.DefaultPort, zpl);
                }
            }

            // PNG：用 Windows 打印机打印，多张放在同一个打印任务里，一张一页（PrintPage 每次画一张，HasMorePages 表示后面还有）。
            // PrintDocument 没有“把图片直接交给它”的方法，要在 PrintPage 里把图画到纸上：按“像素 ÷ DPI”画成标签的实际尺寸（单位是 1/100 英寸），
            // 不能缩放，否则条码扫不出来。纸张大小用打印机驱动里的设置（标签打印机在驱动里设成标签尺寸，A4 拼版用 A4 纸）。
            private static void PrintPng(string name, List<KeyValuePair<string, byte[]>> pages, int dpi)
            {
                using (var document = new PrintDocument())
                {
                    document.DocumentName = name;
                    document.PrintController = new StandardPrintController();   // 不弹“正在打印第几页”的进度框
                    if (PrinterName.Length > 0)
                    {
                        document.PrinterSettings.PrinterName = PrinterName;
                    }

                    var index = 0;
                    document.PrintPage += (sender, e) =>
                    {
                        // 打到哪张才读哪张，几百张也不会占很多内存
                        using (var png = new MemoryStream(pages[index].Value))
                        using (var image = Image.FromStream(png))
                        {
                            e.Graphics.DrawImage(image, 0, 0, image.Width * 100f / dpi, image.Height * 100f / dpi);
                        }

                        index++;
                        e.HasMorePages = index < pages.Count;
                    };
                    document.Print();
                }
            }

            // PDF：.NET 自己读不了 PDF，PrintDocument 也打不了，有两种打法（多页的 PDF 两种都是整份一次打完）：
            //   一、打印机支持 PDF 直接打印（不少办公激光打印机支持，说明书里叫“PDF 直接打印”或 PDF Direct）：在 PdfDirectPrinterName 填它的名称，
            //       像 ZPL 一样把内存里的内容直接发过去，不用存文件，也不用装阅读器；
            //   二、交给本机的 PDF 阅读器打印：要装支持命令行打印的阅读器，如 Adobe Reader、福昕；
            //       默认打开 PDF 的程序不支持打印命令（比如 Edge）时会报 Win32Exception。
            private static void PrintPdf(string name, byte[] pdf)
            {
                if (PdfDirectPrinterName.Length > 0)
                {
                    WindowsRawPrinter.Send(PdfDirectPrinterName, pdf, name);
                    return;
                }

                // 阅读器是另一个程序，只能打开磁盘上的文件，所以先存到临时目录
                var path = Path.Combine(Path.GetTempPath(), name);
                File.WriteAllBytes(path, pdf);
                var start = PrinterName.Length > 0
                    ? new ProcessStartInfo(path) { Verb = "printto", Arguments = "\"" + PrinterName + "\"", UseShellExecute = true }   // 指定打印机
                    : new ProcessStartInfo(path) { Verb = "print", UseShellExecute = true };                                              // Windows 默认打印机
                Process.Start(start);
            }

            // 生成结果里的文件（文件名、内容）：ZIP 按顺序解开，一个条目一个文件；不是 ZIP 时就是结果本身。
            private static List<KeyValuePair<string, byte[]>> Files(LabelFile file)
            {
                var files = new List<KeyValuePair<string, byte[]>>();
                if (!file.IsZip)
                {
                    files.Add(new KeyValuePair<string, byte[]>(file.FileName, file.Content));
                    return files;
                }

                using (var zip = new ZipArchive(file.OpenRead(), ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        using (var stream = entry.Open())
                        using (var content = new MemoryStream())
                        {
                            stream.CopyTo(content);
                            files.Add(new KeyValuePair<string, byte[]>(entry.Name, content.ToArray()));
                        }
                    }
                }

                return files;
            }

        """);
}
