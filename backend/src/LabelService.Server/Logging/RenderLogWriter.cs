using System.Text;
using System.Threading.Channels;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using LabelService.Rendering.Diagnostics;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Logging;

/// <summary>
/// 调用日志的写入队列：请求结束时投递一条，后台服务（<see cref="RenderLogService"/>）按批写入 label-log.db。
/// </summary>
/// <remarks>
/// 投递不等待、不访问数据库（R3）。队列有上限，满了直接丢弃并计数（<see cref="Dropped"/>，也记到指标），不能拖慢请求。
/// </remarks>
internal sealed class RenderLogWriter
{
    private readonly Channel<RenderLogEntity> channel;
    private readonly Action? onDropped;
    private long dropped;

    /// <summary>
    /// 创建队列。
    /// </summary>
    public RenderLogWriter(IOptions<RenderLogOptions> options, Diagnostics.LabelMetrics? metrics = null)
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
    /// 是否写调用日志（模板来源是文件、没有数据目录时关闭）。
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
    /// 投递一条日志。队列满时丢弃，返回 false。
    /// </summary>
    public bool TryWrite(RenderLogEntity record)
    {
        if (!Enabled)
        {
            return false;
        }

        if (channel.Writer.TryWrite(record))
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
    public static string StagesJson(RenderTimings timings)
    {
        var builder = new StringBuilder("{");
        foreach (var (stage, milliseconds) in timings.Stages)
        {
            if (builder.Length > 1)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(stage).Append("\":").Append(Math.Round(milliseconds, 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.Append('}').ToString();
    }
}

/// <summary>
/// 配置节 LabelService:RenderLog。
/// </summary>
internal sealed class RenderLogOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:RenderLog";

    /// <summary>
    /// 是否写调用日志。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 队列上限（条）。满了丢弃。
    /// </summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>
    /// 每批最多写入的条数，一批一个事务。
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// 保留天数，默认 180 天；每天清理一次更早的日志。
    /// </summary>
    public int RetentionDays { get; set; } = 180;
}
