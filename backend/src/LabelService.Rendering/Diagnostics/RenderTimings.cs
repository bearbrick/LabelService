using System.Diagnostics;
using System.Globalization;
using System.Text;
using LabelService.Contracts.Protocol;

namespace LabelService.Rendering.Diagnostics;

/// <summary>
/// 一次请求的分阶段耗时。阶段名用 <see cref="TimingStages"/>，同名阶段累加（批量时逐张累加）。
/// </summary>
/// <remarks>
/// 只在单个请求内使用，不是线程安全的。计时用 <see cref="Stopwatch.GetTimestamp"/>，不要用 DateTime.Now。
/// </remarks>
public sealed class RenderTimings
{
    private readonly List<KeyValuePair<string, double>> stages = new(8);

    /// <summary>
    /// 已记录的阶段及毫秒数，按首次记录的顺序。
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, double>> Stages => stages;

    /// <summary>
    /// 累加一个阶段的耗时。
    /// </summary>
    /// <param name="stage">阶段名。</param>
    /// <param name="milliseconds">毫秒。</param>
    public void Add(string stage, double milliseconds)
    {
        for (var i = 0; i < stages.Count; i++)
        {
            if (stages[i].Key == stage)
            {
                stages[i] = new(stage, stages[i].Value + milliseconds);
                return;
            }
        }

        stages.Add(new(stage, milliseconds));
    }

    /// <summary>
    /// 记录从 <paramref name="startTimestamp"/> 到现在的耗时，并返回当前时间戳，方便连续计时下一阶段。
    /// </summary>
    /// <param name="stage">阶段名。</param>
    /// <param name="startTimestamp">阶段开始时的 <see cref="Stopwatch.GetTimestamp"/>。</param>
    /// <returns>当前时间戳。</returns>
    public long Mark(string stage, long startTimestamp)
    {
        var now = Stopwatch.GetTimestamp();
        Add(stage, Stopwatch.GetElapsedTime(startTimestamp, now).TotalMilliseconds);
        return now;
    }

    /// <summary>
    /// 读取某个阶段的累计毫秒数，没记录过返回 0。
    /// </summary>
    /// <param name="stage">阶段名。</param>
    /// <returns>毫秒。</returns>
    public double Get(string stage)
    {
        foreach (var item in stages)
        {
            if (item.Key == stage)
            {
                return item.Value;
            }
        }

        return 0;
    }

    /// <summary>
    /// 生成 Server-Timing 头的值，如 <c>auth;dur=0.3, tpl;dur=0.1, total;dur=46.1</c>。
    /// 已知阶段按 <see cref="TimingStages.All"/> 的顺序输出，其余阶段排在后面；毫秒保留 1 位小数。
    /// </summary>
    /// <returns>头的值；没有任何阶段时返回空字符串。</returns>
    public string ToServerTimingHeader()
    {
        var builder = new StringBuilder(stages.Count * 18);
        foreach (var stage in TimingStages.All)
        {
            foreach (var item in stages)
            {
                if (item.Key == stage)
                {
                    Append(builder, item);
                }
            }
        }

        foreach (var item in stages)
        {
            if (!TimingStages.All.Contains(item.Key))
            {
                Append(builder, item);
            }
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, KeyValuePair<string, double> item)
    {
        if (builder.Length > 0)
        {
            builder.Append(", ");
        }

        builder.Append(item.Key).Append(";dur=").Append(item.Value.ToString("0.0", CultureInfo.InvariantCulture));
    }
}
