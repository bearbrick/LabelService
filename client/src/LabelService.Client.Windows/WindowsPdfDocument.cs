using System;
using System.Runtime.InteropServices;

namespace LabelService.Client.Printing;

/// <summary>
/// 在专用线程加载一份 PDF，按需渲染单页；所有原生资源在同一线程释放。
/// </summary>
internal sealed class WindowsPdfDocument : IDisposable
{
    /// <summary>
    /// PDF 原生对象使用的专用线程。
    /// </summary>
    private readonly WindowsRuntimeWorker worker;

    /// <summary>
    /// 保存 PDF 内容的 COM 输入流。
    /// </summary>
    private IntPtr input;

    /// <summary>
    /// 输入流的 Windows 随机访问包装。
    /// </summary>
    private IntPtr randomInput;

    /// <summary>
    /// 原生 PDF 文档接口。
    /// </summary>
    private IntPtr document;

    /// <summary>
    /// 是否已经释放。
    /// </summary>
    private bool disposed;

    /// <summary>
    /// 从内存加载 PDF，不写临时文件。
    /// </summary>
    /// <param name="content">PDF 内容。</param>
    public WindowsPdfDocument(byte[] content)
    {
        worker = new WindowsRuntimeWorker();
        try
        {
            PageCount = worker.Invoke(() => Load(content));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// PDF 的实际页数。
    /// </summary>
    public int PageCount
    {
        get;
    }

    /// <summary>
    /// 按生成 DPI 渲染一页，并保留 PDF 页面的准确物理尺寸。
    /// </summary>
    /// <param name="index">从零开始的页号。</param>
    /// <param name="dpi">生成 DPI。</param>
    /// <returns>需要由调用方释放的页面。</returns>
    public PrintablePage Render(int index, int dpi)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsPdfDocument));
        }

        if (index < 0 || index >= PageCount || dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "PDF 页号或渲染 DPI 无效。");
        }

        return worker.Invoke(() => RenderCore(index, dpi));
    }

    /// <summary>
    /// 释放文档、输入流及专用线程，支持重复释放。
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            worker.Invoke(() =>
            {
                Release(ref document);
                Release(ref randomInput);
                Release(ref input);
                return true;
            });
        }
        finally
        {
            worker.Dispose();
        }
    }

    /// <summary>
    /// 在专用线程加载文档并读取页数。
    /// </summary>
    /// <param name="content">PDF 内容。</param>
    /// <returns>页数。</returns>
    private int Load(byte[] content)
    {
        input = WindowsPdfNative.SHCreateMemStream(content, checked((uint)content.Length));
        if (input == IntPtr.Zero)
        {
            throw new OutOfMemoryException("无法为 PDF 创建内存流。");
        }

        var streamId = WindowsPdfNative.RandomStreamId;
        WindowsPdfNative.Check(WindowsPdfNative.CreateRandomAccessStreamOverStream(input, 0, ref streamId, out randomInput));
        var factory = WindowsPdfNative.Activate("Windows.Data.Pdf.PdfDocument", WindowsPdfNative.FactoryId, true);
        IntPtr operation = IntPtr.Zero;
        IntPtr result = IntPtr.Zero;
        try
        {
            WindowsPdfNative.Check(WindowsPdfNative.Method<WindowsPdfNative.WithPointer>(factory, 8)(factory, randomInput, out operation));
            result = WindowsPdfNative.Wait(operation, true);
            document = WindowsPdfNative.Query(result, WindowsPdfNative.DocumentId);
            WindowsPdfNative.Check(WindowsPdfNative.Method<WindowsPdfNative.GetUInt>(document, 7)(document, out var count));
            if (count == 0)
            {
                throw new InvalidOperationException("PDF 中没有可打印页面。");
            }

            return checked((int)count);
        }
        finally
        {
            Release(ref result);
            Release(ref operation);
            Marshal.Release(factory);
        }
    }

    /// <summary>
    /// 渲染单页，读取 PNG 后立即关闭页面和临时输出流。
    /// </summary>
    /// <param name="index">页号。</param>
    /// <param name="dpi">渲染 DPI。</param>
    /// <returns>独立于原生 PDF 对象的图片页面。</returns>
    private PrintablePage RenderCore(int index, int dpi)
    {
        WindowsPdfNative.Check(WindowsPdfNative.Method<WindowsPdfNative.GetPage>(document, 6)(document, (uint)index, out var page));
        IntPtr output = IntPtr.Zero;
        IntPtr randomOutput = IntPtr.Zero;
        IntPtr options = IntPtr.Zero;
        IntPtr operation = IntPtr.Zero;
        try
        {
            WindowsPdfNative.Check(WindowsPdfNative.Method<WindowsPdfNative.GetSize>(page, 10)(page, out var size));
            var width = checked((uint)Math.Max(1, Math.Round(size.Width * dpi / 96d)));
            var height = checked((uint)Math.Max(1, Math.Round(size.Height * dpi / 96d)));
            // 防止损坏或异常大的文档导致单页分配无法承受的位图。
            if (width > 30000 || height > 30000 || (long)width * height > 100000000)
            {
                throw new InvalidOperationException("PDF 页面过大，请降低生成 DPI 或缩小纸张尺寸。");
            }

            options = WindowsPdfNative.Activate("Windows.Data.Pdf.PdfPageRenderOptions", WindowsPdfNative.OptionsId, false);
            WindowsPdfNative.Check(WindowsPdfNative.Method<WindowsPdfNative.SetUInt>(options, 9)(options, width));
            WindowsPdfNative.Check(WindowsPdfNative.Method<WindowsPdfNative.SetUInt>(options, 11)(options, height));
            // 打印内容不跟随桌面高对比度设置变色。
            WindowsPdfNative.Check(WindowsPdfNative.Method<WindowsPdfNative.SetBoolean>(options, 15)(options, true));
            WindowsPdfNative.Check(WindowsPdfNative.CreateStreamOnHGlobal(IntPtr.Zero, true, out output));
            var streamId = WindowsPdfNative.RandomStreamId;
            WindowsPdfNative.Check(WindowsPdfNative.CreateRandomAccessStreamOverStream(output, 0, ref streamId, out randomOutput));
            WindowsPdfNative.Check(WindowsPdfNative.Method<WindowsPdfNative.Render>(page, 7)(page, randomOutput, options, out operation));
            WindowsPdfNative.Wait(operation, false);
            var result = new PrintablePage(WindowsPdfNative.Bytes(output), size.Width / 96f, size.Height / 96f);
            try
            {
                // DestinationWidth/Height 的单位是 DIP，系统可能叠加桌面缩放，打印像素数必须独立校正。
                result.NormalizePixels(checked((int)width), checked((int)height));
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }
        finally
        {
            Release(ref operation);
            Release(ref options);
            Release(ref randomOutput);
            Release(ref output);
            WindowsPdfNative.ClosePage(page);
        }
    }

    /// <summary>
    /// 成对释放原生接口并清空字段。
    /// </summary>
    /// <param name="pointer">接口引用。</param>
    private static void Release(ref IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.Release(pointer);
            pointer = IntPtr.Zero;
        }
    }
}
