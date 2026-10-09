using System.Diagnostics;
using LabelService.Server.Diagnostics;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Hosting;

/// <summary>
/// 渲染并发限制（设计书第 9 节、T-017）：同时渲染的请求数等于 CPU 核数，多出的排队；排队超过上限时放弃，接口返回 503。
/// </summary>
/// <remarks>
/// 渲染是纯 CPU 计算，并发超过核数只会让所有请求一起变慢；排队让先到的先完成。
/// 一个请求（单张或批量）占一个名额，排队时间记到 label_queue_wait_ms。
/// </remarks>
internal sealed class RenderGate : IDisposable
{
    private readonly SemaphoreSlim slots;
    private readonly TimeSpan timeout;
    private readonly LabelMetrics metrics;

    /// <summary>
    /// 按配置创建。
    /// </summary>
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
    public async ValueTask<IDisposable?> EnterAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var entered = slots.Wait(0) || await slots.WaitAsync(timeout, cancellationToken);
        metrics.QueueWaited(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return entered ? new Lease(slots) : null;
    }

    /// <inheritdoc />
    public void Dispose() => slots.Dispose();

    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                slots.Release();
            }
        }
    }
}

/// <summary>
/// 配置节 LabelService:Render。
/// </summary>
internal sealed class RenderOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Render";

    /// <summary>
    /// 同时渲染的请求数；0 表示等于 CPU 核数。
    /// </summary>
    public int MaxConcurrency
    {
        get; set;
    }

    /// <summary>
    /// 排队等待的上限，毫秒；超过返回 503。默认 2000。
    /// </summary>
    public int QueueTimeoutMs { get; set; } = 2000;
}
