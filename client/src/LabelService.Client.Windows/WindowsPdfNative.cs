using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using STATSTG = System.Runtime.InteropServices.ComTypes.STATSTG;

namespace LabelService.Client.Printing;

/// <summary>
/// Windows.Data.Pdf 的原生 ABI 桥接；不加载阅读器或第三方 PDF 库。
/// </summary>
/// <remarks>
/// 接口顺序依据微软 Windows SDK 定义。直接调用 ABI，使 net461 和现代 .NET 使用同一份代码。
/// IInspectable 的前六个槽是基础方法；各接口的业务方法从第六槽开始。
/// </remarks>
internal static class WindowsPdfNative
{
    /// <summary>
    /// Windows 随机访问流接口。
    /// </summary>
    internal static readonly Guid RandomStreamId = new Guid("905a0fe1-bc53-11df-8c49-001e4fc686da");

    /// <summary>
    /// PDF 静态工厂接口。
    /// </summary>
    internal static readonly Guid FactoryId = new Guid("433a0b5f-c007-4788-90f2-08143d922599");

    /// <summary>
    /// PDF 文档接口。
    /// </summary>
    internal static readonly Guid DocumentId = new Guid("ac7ebedd-80fa-4089-846e-81b77ff5a86c");

    /// <summary>
    /// PDF 渲染参数接口。
    /// </summary>
    internal static readonly Guid OptionsId = new Guid("3c98056f-b7cf-4c29-9a04-52d90267f425");

    /// <summary>
    /// 异步操作状态接口。
    /// </summary>
    private static readonly Guid AsyncInfoId = new Guid("00000036-0000-0000-c000-000000000046");

    /// <summary>
    /// 可关闭资源接口。
    /// </summary>
    private static readonly Guid ClosableId = new Guid("30d5a829-7fa4-4026-83bb-d75bae4ea99e");

    /// <summary>
    /// 读取一个接口指针的原生方法。
    /// </summary>
    /// <param name="self">接口指针。</param>
    /// <param name="value">返回的接口。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetPointer(IntPtr self, out IntPtr value);

    /// <summary>
    /// 传入一个接口并返回一个接口的原生方法。
    /// </summary>
    /// <param name="self">接口指针。</param>
    /// <param name="input">输入接口。</param>
    /// <param name="value">返回接口。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int WithPointer(IntPtr self, IntPtr input, out IntPtr value);

    /// <summary>
    /// 传入两个接口并返回异步操作的原生方法。
    /// </summary>
    /// <param name="self">接口指针。</param>
    /// <param name="output">输出流。</param>
    /// <param name="options">渲染参数。</param>
    /// <param name="value">异步操作。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int Render(IntPtr self, IntPtr output, IntPtr options, out IntPtr value);

    /// <summary>
    /// 按页号取得页面的原生方法。
    /// </summary>
    /// <param name="self">文档接口。</param>
    /// <param name="index">从零开始的页号。</param>
    /// <param name="value">页面接口。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetPage(IntPtr self, uint index, out IntPtr value);

    /// <summary>
    /// 读取无符号整数的原生方法。
    /// </summary>
    /// <param name="self">接口。</param>
    /// <param name="value">返回值。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetUInt(IntPtr self, out uint value);

    /// <summary>
    /// 写入无符号整数的原生方法。
    /// </summary>
    /// <param name="self">接口。</param>
    /// <param name="value">参数值。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int SetUInt(IntPtr self, uint value);

    /// <summary>
    /// 写入 Windows 运行时的一字节布尔值。
    /// </summary>
    /// <param name="self">接口。</param>
    /// <param name="value">参数值。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int SetBoolean(IntPtr self, [MarshalAs(UnmanagedType.I1)] bool value);

    /// <summary>
    /// 读取页面尺寸的原生方法。
    /// </summary>
    /// <param name="self">页面接口。</param>
    /// <param name="value">以九十六 DPI 为基准的尺寸。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetSize(IntPtr self, out PdfSize value);

    /// <summary>
    /// 无参数的原生方法。
    /// </summary>
    /// <param name="self">接口。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int Simple(IntPtr self);

    /// <summary>
    /// 读取 COM 流信息的原生方法。
    /// </summary>
    /// <param name="self">流接口。</param>
    /// <param name="value">流信息。</param>
    /// <param name="flags">不返回名称时为一。</param>
    /// <returns>HRESULT。</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Stat(IntPtr self, out STATSTG value, uint flags);

    /// <summary>
    /// Windows 页面尺寸的 ABI 布局。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PdfSize
    {
        /// <summary>
        /// 页面宽度。
        /// </summary>
        internal float Width;

        /// <summary>
        /// 页面高度。
        /// </summary>
        internal float Height;
    }

    /// <summary>
    /// 取得接口表中的方法；调用期间由拥有该指针的对象保持原生资源存活。
    /// </summary>
    /// <typeparam name="T">原生委托类型。</typeparam>
    /// <param name="pointer">接口指针。</param>
    /// <param name="slot">方法槽号。</param>
    /// <returns>原生方法。</returns>
    internal static T Method<T>(IntPtr pointer, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * IntPtr.Size));

    /// <summary>
    /// 核对 HRESULT，不把原生失败伪装成成功。
    /// </summary>
    /// <param name="result">HRESULT。</param>
    internal static void Check(int result)
    {
        if (result < 0)
        {
            throw new COMException("Windows PDF 操作失败，错误码 0x" + result.ToString("X8") + "。", result);
        }
    }

    /// <summary>
    /// 取得另一个接口，返回值由调用方释放。
    /// </summary>
    /// <param name="pointer">原接口。</param>
    /// <param name="id">需要的接口。</param>
    /// <returns>新的接口引用。</returns>
    internal static IntPtr Query(IntPtr pointer, Guid id)
    {
        Check(Marshal.QueryInterface(pointer, ref id, out var value));
        return value;
    }

    /// <summary>
    /// 激活 Windows PDF 工厂或渲染参数。
    /// </summary>
    /// <param name="name">系统运行时类名。</param>
    /// <param name="id">需要的接口。</param>
    /// <param name="factory">是否需要静态工厂。</param>
    /// <returns>需要的接口引用。</returns>
    internal static IntPtr Activate(string name, Guid id, bool factory)
    {
        Check(WindowsCreateString(name, name.Length, out var text));
        try
        {
            if (factory)
            {
                var result = RoGetActivationFactory(text, ref id, out var value);
                if (result == unchecked((int)0x80040154))
                {
                    throw new PlatformNotSupportedException("系统没有 PDF 渲染接口，请使用 Windows 8.1 或更新版本。");
                }

                Check(result);
                return value;
            }

            Check(RoActivateInstance(text, out var instance));
            try
            {
                return Query(instance, id);
            }
            finally
            {
                Marshal.Release(instance);
            }
        }
        finally
        {
            WindowsDeleteString(text);
        }
    }

    /// <summary>
    /// 等待原生异步操作，失败或超过三十秒时向调用方报告，不依赖界面消息循环。
    /// </summary>
    /// <param name="operation">异步操作。</param>
    /// <param name="hasResult">是否返回文档接口。</param>
    /// <returns>文档接口，或零指针。</returns>
    internal static IntPtr Wait(IntPtr operation, bool hasResult)
    {
        var info = Query(operation, AsyncInfoId);
        try
        {
            var timer = Stopwatch.StartNew();
            while (true)
            {
                Check(Method<GetUInt>(info, 7)(info, out var status));
                if (status == 1)
                {
                    if (hasResult)
                    {
                        Check(Method<GetPointer>(operation, 8)(operation, out var result));
                        return result;
                    }

                    Check(Method<Simple>(operation, 8)(operation));
                    return IntPtr.Zero;
                }

                if (status != 0)
                {
                    Check(Method<GetUInt>(info, 8)(info, out var error));
                    Check(unchecked((int)error));
                    throw new OperationCanceledException("Windows PDF 操作已取消。");
                }

                if (timer.Elapsed > TimeSpan.FromSeconds(30))
                {
                    Method<Simple>(info, 9)(info);
                    throw new TimeoutException("Windows PDF 处理超过三十秒，请检查 PDF 内容或缩小页面。");
                }

                Thread.Sleep(5);
            }
        }
        finally
        {
            // 操作引用由调用方释放；此处只释放查询取得的状态接口。
            Marshal.Release(info);
        }
    }

    /// <summary>
    /// 读取 HGLOBAL 流的实际内容，不读取分配容量中的尾部空白。
    /// </summary>
    /// <param name="stream">CreateStreamOnHGlobal 创建的流。</param>
    /// <returns>流内容。</returns>
    internal static byte[] Bytes(IntPtr stream)
    {
        Check(Method<Stat>(stream, 12)(stream, out var stat, 1));
        var length = checked((int)stat.cbSize);
        Check(GetHGlobalFromStream(stream, out var memory));
        var pointer = GlobalLock(memory);
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("无法读取 PDF 页面图像。");
        }

        try
        {
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return bytes;
        }
        finally
        {
            GlobalUnlock(memory);
        }
    }

    /// <summary>
    /// 关闭页面的内部资源，并释放接口。
    /// </summary>
    /// <param name="page">页面接口。</param>
    internal static void ClosePage(IntPtr page)
    {
        var closable = IntPtr.Zero;
        try
        {
            closable = Query(page, ClosableId);
            Check(Method<Simple>(closable, 6)(closable));
        }
        finally
        {
            if (closable != IntPtr.Zero)
            {
                Marshal.Release(closable);
            }
            Marshal.Release(page);
        }
    }

    /// <summary>
    /// 初始化当前线程的 Windows 运行时。
    /// </summary>
    /// <param name="type">MTA 初始化类型。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("combase.dll")]
    internal static extern int RoInitialize(uint type);

    /// <summary>
    /// 关闭当前线程的 Windows 运行时。
    /// </summary>
    [DllImport("combase.dll")]
    internal static extern void RoUninitialize();

    /// <summary>
    /// 创建内存输入流，内容由系统复制。
    /// </summary>
    /// <param name="content">内容。</param>
    /// <param name="length">字节数。</param>
    /// <returns>COM 流接口。</returns>
    [DllImport("shlwapi.dll")]
    internal static extern IntPtr SHCreateMemStream(byte[] content, uint length);

    /// <summary>
    /// 把 COM 流包装成 Windows 随机访问流。
    /// </summary>
    /// <param name="stream">COM 流。</param>
    /// <param name="options">流包装选项。</param>
    /// <param name="id">随机访问流接口。</param>
    /// <param name="result">包装后的接口。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("shcore.dll")]
    internal static extern int CreateRandomAccessStreamOverStream(IntPtr stream, uint options, ref Guid id, out IntPtr result);

    /// <summary>
    /// 创建可自动释放内存的输出流。
    /// </summary>
    /// <param name="memory">已有内存，零表示创建新内存。</param>
    /// <param name="deleteOnRelease">释放流时释放内存。</param>
    /// <param name="stream">输出流。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("ole32.dll")]
    internal static extern int CreateStreamOnHGlobal(IntPtr memory, [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease, out IntPtr stream);

    /// <summary>
    /// 创建运行时字符串。
    /// </summary>
    /// <param name="value">字符串。</param>
    /// <param name="length">字符数。</param>
    /// <param name="result">运行时字符串。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string value, int length, out IntPtr result);

    /// <summary>
    /// 释放运行时字符串。
    /// </summary>
    /// <param name="value">字符串。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr value);

    /// <summary>
    /// 取得系统运行时工厂。
    /// </summary>
    /// <param name="name">运行时类名。</param>
    /// <param name="id">需要的接口。</param>
    /// <param name="result">工厂接口。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr name, ref Guid id, out IntPtr result);

    /// <summary>
    /// 创建系统运行时对象。
    /// </summary>
    /// <param name="name">运行时类名。</param>
    /// <param name="result">运行时对象。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("combase.dll")]
    private static extern int RoActivateInstance(IntPtr name, out IntPtr result);

    /// <summary>
    /// 取得流的内存句柄。
    /// </summary>
    /// <param name="stream">COM 流。</param>
    /// <param name="memory">内存句柄。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("ole32.dll")]
    private static extern int GetHGlobalFromStream(IntPtr stream, out IntPtr memory);

    /// <summary>
    /// 锁定可移动内存。
    /// </summary>
    /// <param name="memory">内存句柄。</param>
    /// <returns>数据指针。</returns>
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr memory);

    /// <summary>
    /// 解除内存锁定。
    /// </summary>
    /// <param name="memory">内存句柄。</param>
    /// <returns>系统锁定状态。</returns>
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);
}
