using System.Globalization;
using System.Text;
using System.Threading.Channels;
using LabelService.Application.Diagnostics;
using LabelService.Domain.Logs;
using LabelService.Rendering.Diagnostics;
using Microsoft.Extensions.Options;

namespace LabelService.Application.Logs;

/// <summary>
/// 调用日志的写入队列：请求结束时投递一条，基础设施层的后台服务按批写进日志库。
/// </summary>
/// <remarks>
/// 投递不等待、不访问数据库（AGENTS.md R3）。队列有上限，满了直接丢弃并计数（<see cref="Dropped"/>，也记到指标），不能拖慢请求。
/// </remarks>
public sealed class RenderLogQueue
{
    /// <summary>
    /// 有界队列，多个请求写、一个后台服务读。
    /// </summary>
    private readonly Channel<RenderLogEntity> channel;

    /// <summary>
    /// 丢弃一条时调用（记指标）；没有指标时为 null。
    /// </summary>
    private readonly Action? onDropped;

    /// <summary>
    /// 因队列满丢弃的条数。
    /// </summary>
    private long dropped;

    /// <summary>
    /// 创建队列。
    /// </summary>
    /// <param name="options">配置节 LabelService:RenderLog。</param>
    /// <param name="metrics">指标，丢弃时计数；检查程序里可以不传。</param>
    public RenderLogQueue(IOptions<RenderLogOptions> options, LabelMetrics? metrics = null)
    {
        Enabled = options.Value.Enabled;
        channel = Channel.CreateBounded<RenderLogEntity>(new BoundedChannelOptions(Math.Max(1, options.Value.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        onDropped = metrics is null ? null : metrics.RenderLogDropped;
    }

    /// <summary>
    /// 是否写调用日志（模板来源是文件、没有数据库时关闭）。
    /// </summary>
    public bool Enabled
    {
        get; set;
    }

    /// <summary>
    /// 因队列满丢弃的条数。
    /// </summary>
    public long Dropped => Interlocked.Read(ref dropped);

    /// <summary>
    /// 队列里等着写入的条数。
    /// </summary>
    public int Pending => channel.Reader.Count;

    /// <summary>
    /// 给后台服务读取。
    /// </summary>
    public ChannelReader<RenderLogEntity> Reader => channel.Reader;

    /// <summary>
    /// 投递一条日志。队列满时丢弃。
    /// </summary>
    /// <param name="entity">一条调用日志。</param>
    /// <returns>放进队列时为 true；关闭或队列满时为 false。</returns>
    public bool TryWrite(RenderLogEntity entity)
    {
        if (!Enabled)
        {
            return false;
        }

        if (channel.Writer.TryWrite(entity))
        {
            return true;
        }

        Interlocked.Increment(ref dropped);
        onDropped?.Invoke();
        return false;
    }

    /// <summary>
    /// 各阶段耗时写成紧凑的 JSON，如 {"auth":0.3,"layout":4.2}。阶段名是固定的 ASCII，不需要转义。
    /// </summary>
    /// <param name="timings">分阶段耗时。</param>
    /// <returns>JSON 对象字符串。</returns>
    public static string StagesJson(RenderTimings timings)
    {
        var builder = new StringBuilder("{");
        foreach (var (stage, milliseconds) in timings.Stages)
        {
            if (builder.Length > 1)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(stage).Append("\":").Append(Math.Round(milliseconds, 1).ToString(CultureInfo.InvariantCulture));
        }

        return builder.Append('}').ToString();
    }
}
