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

    private static class NativeMethods
    {
        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool ClosePrinter(IntPtr printer);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int StartDocPrinter(IntPtr printer, int level, [In] DocInfo1 document);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndDocPrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool StartPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool WritePrinter(IntPtr printer, IntPtr bytes, int count, out int written);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public sealed class DocInfo1
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string DocumentName = "";

            [MarshalAs(UnmanagedType.LPWStr)]
            public string? OutputFile;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string DataType = "RAW";
        }
    }
}
