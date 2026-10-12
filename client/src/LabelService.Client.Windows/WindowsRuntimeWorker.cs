using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace LabelService.Client.Printing;

/// <summary>
/// 在专用 MTA 线程使用 Windows PDF 接口，避免在界面 STA 线程等待原生异步操作。
/// </summary>
internal sealed class WindowsRuntimeWorker : IDisposable
{
    /// <summary>
    /// 按顺序在同一个线程执行的操作。
    /// </summary>
    private readonly BlockingCollection<Action> requests = new BlockingCollection<Action>();

    /// <summary>
    /// 原生运行时初始化结果。
    /// </summary>
    private readonly TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();

    /// <summary>
    /// 专用后台线程。
    /// </summary>
    private readonly Thread thread;

    /// <summary>
    /// 是否已关闭。
    /// </summary>
    private bool disposed;

    /// <summary>
    /// 创建并初始化专用线程；初始化失败时立即清理。
    /// </summary>
    public WindowsRuntimeWorker()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "LabelService PDF" };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        try
        {
            ready.Task.GetAwaiter().GetResult();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// 执行操作并把结果或原异常交给调用方。
    /// </summary>
    /// <typeparam name="T">结果类型。</typeparam>
    /// <param name="operation">原生接口操作。</param>
    /// <returns>操作结果。</returns>
    public T Invoke<T>(Func<T> operation)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsRuntimeWorker));
        }

        var result = new TaskCompletionSource<T>();
        requests.Add(() =>
        {
            try
            {
                result.SetResult(operation());
            }
            catch (Exception exception)
            {
                // 跨线程保留异常类型和堆栈，由调用方决定提示或重试。
                result.SetException(exception);
            }
        });
        return result.Task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// 结束队列并等待原生运行时退出。
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        requests.CompleteAdding();
        thread.Join();
        requests.Dispose();
    }

    /// <summary>
    /// 初始化 Windows 运行时，逐个执行操作，结束时成对关闭运行时。
    /// </summary>
    private void Run()
    {
        try
        {
            WindowsPdfNative.Check(WindowsPdfNative.RoInitialize(1));
        }
        catch (Exception exception)
        {
            ready.SetException(exception is DllNotFoundException || exception is EntryPointNotFoundException
                ? new PlatformNotSupportedException("PDF 静默打印需要 Windows 8.1 或更新版本。", exception)
                : exception);
            return;
        }

        try
        {
            ready.SetResult(true);
            foreach (var operation in requests.GetConsumingEnumerable())
            {
                operation();
            }
        }
        finally
        {
            WindowsPdfNative.RoUninitialize();
        }
    }
}
