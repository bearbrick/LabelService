using System.Diagnostics;
using LabelService.Application.Diagnostics;
using Microsoft.Extensions.Options;

namespace LabelService.Application.Rendering;

/// <summary>
/// 渲染并发限制（设计书第 9 节）：同时渲染的请求数等于 CPU 核数，多出的排队；排队超过上限时放弃，接口返回 503。
/// </summary>
/// <remarks>
/// 渲染是纯 CPU 计算，并发超过核数只会让所有请求一起变慢；排队让先到的先完成。
/// 一个请求（单张或批量）占一个名额，排队时间记到指标 label_queue_wait_ms。
/// </remarks>
public sealed class RenderGate : IDisposable
{
    /// <summary>
    /// 名额。
    /// </summary>
    private readonly SemaphoreSlim slots;

    /// <summary>
    /// 排队等待的上限。
    /// </summary>
    private readonly TimeSpan timeout;

    /// <summary>
    /// 指标，记排队时间。
    /// </summary>
    private readonly LabelMetrics metrics;

    /// <summary>
    /// 按配置创建。
    /// </summary>
    /// <param name="options">配置节 LabelService:Render。</param>
    /// <param name="metrics">指标。</param>
    public RenderGate(IOptions<RenderOptions> options, LabelMetrics metrics)
    {
        var value = options.Value;
        Capacity = value.MaxConcurrency > 0 ? value.MaxConcurrency : Environment.ProcessorCount;
        slots = new SemaphoreSlim(Capacity, Capacity);
        timeout = TimeSpan.FromMilliseconds(Math.Max(0, value.QueueTimeoutMs));
        this.metrics = metrics;
    }

    /// <summary>
    /// 同时渲染的上限。
    /// </summary>
    public int Capacity
    {
        get;
    }

    /// <summary>
    /// 排队等待的上限（秒），写进 503 的提示里。
    /// </summary>
    public double TimeoutSeconds => timeout.TotalSeconds;

    /// <summary>
    /// 排队拿一个名额；超时返回 null。拿到的名额用完必须 Dispose。
    /// </summary>
    /// <param name="cancellationToken">请求取消（客户端断开）。</param>
    /// <returns>名额；超时为 null。</returns>
    public async ValueTask<IDisposable?> EnterAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        // 先试一下不等待就拿到（大多数情况），拿不到再异步排队。
        var entered = slots.Wait(0) || await slots.WaitAsync(timeout, cancellationToken);
        metrics.QueueWaited(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return entered ? new Lease(slots) : null;
    }

    /// <inheritdoc />
    public void Dispose() => slots.Dispose();

    /// <summary>
    /// 一个名额；Dispose 时归还，重复 Dispose 只归还一次。
    /// </summary>
    /// <param name="slots">名额所在的信号量。</param>
    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    {
        /// <summary>
        /// 是否已经归还（0 没有，1 已归还）。
        /// </summary>
        private int released;

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                slots.Release();
            }
        }
    }
}
