using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LabelService.Client.Printing;

/// <summary>
/// 以 RAW 方式把打印机指令写入 Windows 打印队列，用于 USB 连接、已安装驱动的标签打印机。仅限 Windows。
/// </summary>
public static class WindowsRawPrinter
{
    /// <summary>
    /// 发送指令到打印队列。
    /// </summary>
    /// <param name="printerName">控制面板里的打印机名称。</param>
    /// <param name="content">要发送的字节，如 ZPL。</param>
    /// <param name="documentName">打印队列里显示的文档名。</param>
    /// <exception cref="PlatformNotSupportedException">不是 Windows。</exception>
    /// <exception cref="Win32Exception">打印机不存在或写入失败。</exception>
    public static void Send(string printerName, byte[] content, string documentName = "Label")
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            throw new PlatformNotSupportedException("WindowsRawPrinter 只能在 Windows 上使用。");
        }

        if (printerName == null || printerName.Trim().Length == 0)
        {
            throw new ArgumentException("必须指定打印机名称。", nameof(printerName));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        // 打开打印机 → 开始作业（RAW，不经过驱动渲染）→ 开始一页 → 写数据 → 逐层结束；每一步失败都带上 Win32 错误码。
        if (!NativeMethods.OpenPrinter(printerName, out var printer, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法打开打印机 {printerName}。");
        }

        try
        {
            var document = new NativeMethods.DocInfo1 { DocumentName = documentName, DataType = "RAW" };
            if (NativeMethods.StartDocPrinter(printer, 1, document) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "StartDocPrinter 失败。");
            }

            try
            {
                if (!NativeMethods.StartPagePrinter(printer))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "StartPagePrinter 失败。");
                }

                // WritePrinter 要非托管内存，复制过去，用完释放。
                var buffer = Marshal.AllocCoTaskMem(content.Length);
                try
                {
                    Marshal.Copy(content, 0, buffer, content.Length);
                    if (!NativeMethods.WritePrinter(printer, buffer, content.Length, out var written) || written != content.Length)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "WritePrinter 失败。");
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(buffer);
                    NativeMethods.EndPagePrinter(printer);
                }
            }
            finally
            {
                NativeMethods.EndDocPrinter(printer);
            }
        }
        finally
        {
            NativeMethods.ClosePrinter(printer);
        }
    }

    /// <summary>
    /// Windows 打印后台程序（winspool.drv）的原生函数。
    /// </summary>
    private static class NativeMethods
    {
        /// <summary>
        /// 按名称打开打印机。
        /// </summary>
        /// <param name="printerName">打印机名称（控制面板里显示的名字）。</param>
        /// <param name="printer">打开的打印机句柄。</param>
        /// <param name="defaults">默认设置；传 IntPtr.Zero。</param>
        /// <returns>成功时为 true；失败时用 Marshal.GetLastWin32Error 取错误码。</returns>
        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

        /// <summary>
        /// 关闭打印机句柄。
        /// </summary>
        /// <param name="printer">打印机句柄。</param>
        /// <returns>成功时为 true。</returns>
        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool ClosePrinter(IntPtr printer);

        /// <summary>
        /// 开始一个打印作业。
        /// </summary>
        /// <param name="printer">打印机句柄。</param>
        /// <param name="level">document 结构的级别，固定为 1。</param>
        /// <param name="document">作业名和数据类型（RAW）。</param>
        /// <returns>作业 ID；失败时为 0。</returns>
        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int StartDocPrinter(IntPtr printer, int level, [In] DocInfo1 document);

        /// <summary>
        /// 结束打印作业。
        /// </summary>
        /// <param name="printer">打印机句柄。</param>
        /// <returns>成功时为 true。</returns>
        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndDocPrinter(IntPtr printer);

        /// <summary>
        /// 开始一页。
        /// </summary>
        /// <param name="printer">打印机句柄。</param>
        /// <returns>成功时为 true。</returns>
        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool StartPagePrinter(IntPtr printer);

        /// <summary>
        /// 结束一页。
        /// </summary>
        /// <param name="printer">打印机句柄。</param>
        /// <returns>成功时为 true。</returns>
        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndPagePrinter(IntPtr printer);

        /// <summary>
        /// 把原始数据写给打印机。
        /// </summary>
        /// <param name="printer">打印机句柄。</param>
        /// <param name="bytes">数据的非托管内存地址。</param>
        /// <param name="count">字节数。</param>
        /// <param name="written">实际写入的字节数。</param>
        /// <returns>成功时为 true。</returns>
        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool WritePrinter(IntPtr printer, IntPtr bytes, int count, out int written);

        /// <summary>
        /// DOC_INFO_1 结构：打印作业的名称、输出文件和数据类型。
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public sealed class DocInfo1
        {
            /// <summary>
            /// 作业名，显示在打印队列里。
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string DocumentName = "";

            /// <summary>
            /// 输出文件；null 表示直接送打印机。
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? OutputFile;

            /// <summary>
            /// 数据类型；RAW 表示不经过驱动渲染，原样发给打印机。
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string DataType = "RAW";
        }
    }
}
