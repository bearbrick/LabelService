using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

using LabelService.Application.Diagnostics;

namespace LabelService.Api.Diagnostics;

/// <summary>
/// 把 .NET 自带的指标（System.Diagnostics.Metrics）汇总成 Prometheus 文本格式，给 <c>/metrics</c> 用（ADR 0009）。
/// </summary>
/// <remarks>
/// <para>
/// 订阅三类 Meter：本服务的 <c>LabelService</c>（<see cref="LabelMetrics"/>）、ASP.NET Core 的 <c>Microsoft.AspNetCore.Hosting</c>
/// （这两个只认本进程这个主机创建的，检查程序里同时有多个主机也不串）、运行时的 <c>System.Runtime</c>（CPU、内存、GC、线程池）。
/// </para>
/// <para>
/// 计数器累加，UpDownCounter 和仪表取当前值，直方图按固定分桶累计。本服务的指标名已经是 Prometheus 风格，原样导出；
/// 其他 Meter 的名字把点换成下划线，按单位加 <c>_seconds</c>、<c>_bytes</c> 后缀，计数器加 <c>_total</c>。
/// </para>
/// <para>
/// 记录发生在请求路径上：每次只是查一个序列、加锁累加，没有 I/O。
/// </para>
/// </remarks>
internal sealed class PrometheusExporter : IDisposable
{
    /// <summary>
    /// 运行时指标的 Meter 名（.NET 9 起内置）。
    /// </summary>
    public const string RuntimeMeter = "System.Runtime";

    /// <summary>
    /// ASP.NET Core 请求指标的 Meter 名。
    /// </summary>
    public const string HostingMeter = "Microsoft.AspNetCore.Hosting";

    /// <summary>
    /// 单位是毫秒的直方图的分桶边界（本服务的耗时指标），覆盖 1 ms 到 5 s，100 ms 目标附近分得细一些。
    /// </summary>
    private static readonly double[] MillisecondBuckets = [1, 2, 5, 10, 20, 30, 50, 75, 100, 150, 200, 300, 500, 1000, 2000, 5000];

    /// <summary>
    /// 单位是秒的直方图的分桶边界（ASP.NET Core 的请求耗时）。
    /// </summary>
    private static readonly double[] SecondBuckets = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];

    /// <summary>
    /// 订阅指标的监听器。
    /// </summary>
    private readonly MeterListener listener = new();

    /// <summary>
    /// 每个仪器（指标）对应的序列集合。
    /// </summary>
    private readonly ConcurrentDictionary<Instrument, Family> families = new();

    /// <summary>
    /// 抓取之间的锁：读可观测仪表和拼文本不能并发，否则两次抓取的值会互相覆盖。
    /// </summary>
    private readonly Lock scrape = new();

    /// <summary>
    /// 开始监听。
    /// </summary>
    /// <param name="meterFactory">本主机的 Meter 工厂，用来区分同一进程里其他主机（如检查程序同时启动的多个服务）的指标。</param>
    public PrometheusExporter(IMeterFactory meterFactory)
    {
        listener.InstrumentPublished = (instrument, meters) =>
        {
            // 运行时指标全进程只有一份；本服务和 ASP.NET Core 的指标只认本主机创建的。
            var meter = instrument.Meter;
            var ours = meter.Name == RuntimeMeter
                || (meter.Name is LabelMetrics.MeterName or HostingMeter && ReferenceEquals(meter.Scope, meterFactory));
            if (ours)
            {
                var family = families.GetOrAdd(instrument, item => new Family(item));
                meters.EnableMeasurementEvents(instrument, family);
            }
        };
        listener.MeasurementsCompleted = (instrument, _) => families.TryRemove(instrument, out Family? _);
        listener.SetMeasurementEventCallback<double>((_, value, tags, state) => ((Family)state!).Record(value, tags));
        listener.SetMeasurementEventCallback<float>((_, value, tags, state) => ((Family)state!).Record(value, tags));
        listener.SetMeasurementEventCallback<long>((_, value, tags, state) => ((Family)state!).Record(value, tags));
        listener.SetMeasurementEventCallback<int>((_, value, tags, state) => ((Family)state!).Record(value, tags));
        listener.Start();
    }

    /// <summary>
    /// 指标类型。
    /// </summary>
    internal enum Kind
    {
        /// <summary>
        /// 只增的计数器（Counter、ObservableCounter）。
        /// </summary>
        Counter,

        /// <summary>
        /// 当前值（UpDownCounter、Gauge 和对应的可观测版本）。
        /// </summary>
        Gauge,

        /// <summary>
        /// 直方图。
        /// </summary>
        Histogram,
    }

    /// <summary>
    /// 生成 Prometheus 文本格式（0.0.4）。可观测的仪表在这时读一次当前值。
    /// </summary>
    /// <returns>全部指标的文本，按指标名排序。</returns>
    public string Scrape()
    {
        lock (scrape)
        {
            listener.RecordObservableInstruments();
            var text = new StringBuilder(16 * 1024);
            foreach (var family in families.Values.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                family.Write(text);
            }

            return text.ToString();
        }
    }

    /// <inheritdoc />
    public void Dispose() => listener.Dispose();

    /// <summary>
    /// Prometheus 的指标名：本服务的原样用；其他 Meter 的把非法字符换成下划线，按单位加后缀，计数器加 _total。
    /// </summary>
    /// <param name="instrument">仪器。</param>
    /// <param name="kind">指标类型。</param>
    /// <returns>如 label_render_duration_ms、http_server_request_duration_seconds、dotnet_gc_collections_total。</returns>
    internal static string MetricName(Instrument instrument, Kind kind)
    {
        var name = Sanitize(instrument.Name);
        if (instrument.Meter.Name == LabelMetrics.MeterName)
        {
            return name;
        }

        var unit = instrument.Unit switch
        {
            "s" => "seconds",
            "ms" => "milliseconds",
            "By" => "bytes",
            _ => null,
        };
        if (unit is not null && !name.EndsWith("_" + unit, StringComparison.Ordinal))
        {
            name += "_" + unit;
        }

        return kind == Kind.Counter && !name.EndsWith("_total", StringComparison.Ordinal) ? name + "_total" : name;
    }

    /// <summary>
    /// 把名字里 Prometheus 不允许的字符（字母、数字、下划线以外的）换成下划线。
    /// </summary>
    /// <param name="name">指标名或维度名，如 http.server.request.duration。</param>
    /// <returns>如 http_server_request_duration。</returns>
    private static string Sanitize(string name)
    {
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!(char.IsAsciiLetterOrDigit(chars[i]) || chars[i] == '_'))
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// 维度值的转义：反斜杠、双引号、换行。
    /// </summary>
    /// <param name="value">维度值。</param>
    /// <returns>可以放进双引号里的文本。</returns>
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>
    /// 数值的写法：无穷大写成 +Inf、-Inf，其余用往返格式（不丢精度）。
    /// </summary>
    /// <param name="value">数值。</param>
    /// <returns>Prometheus 认识的数值文本。</returns>
    private static string Number(double value) =>
        double.IsPositiveInfinity(value) ? "+Inf" : double.IsNegativeInfinity(value) ? "-Inf" : value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// 一个指标的全部序列（不同维度组合）。
    /// </summary>
    private sealed class Family
    {
        /// <summary>
        /// 维度组合（排好序的 a="1",b="2"）→ 序列。
        /// </summary>
        private readonly ConcurrentDictionary<string, Series> series = new(StringComparer.Ordinal);

        /// <summary>
        /// 是否可观测的仪器（抓取时回调报告当前值或累计值，直接覆盖，不累加）。
        /// </summary>
        private readonly bool observable;

        /// <summary>
        /// 直方图的分桶边界（按单位选）。
        /// </summary>
        private readonly double[] buckets;

        /// <summary>
        /// # HELP 行的说明（仪器的描述，转义过）。
        /// </summary>
        private readonly string help;

        /// <summary>
        /// 按仪器的类型确定指标类型、名字、分桶和说明。
        /// </summary>
        /// <param name="instrument">仪器。</param>
        public Family(Instrument instrument)
        {
            var type = instrument.GetType();
            var generic = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            observable = generic == typeof(ObservableCounter<>) || generic == typeof(ObservableUpDownCounter<>) || generic == typeof(ObservableGauge<>);
            Kind = generic == typeof(Histogram<>) ? Kind.Histogram
                : generic == typeof(Counter<>) || generic == typeof(ObservableCounter<>) ? Kind.Counter
                : Kind.Gauge;
            Name = MetricName(instrument, Kind);
            buckets = instrument.Unit == "s" ? SecondBuckets : MillisecondBuckets;
            help = (instrument.Description ?? instrument.Name).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        }

        /// <summary>
        /// Prometheus 的指标名。
        /// </summary>
        public string Name
        {
            get;
        }

        /// <summary>
        /// 指标类型。
        /// </summary>
        public Kind Kind
        {
            get;
        }

        /// <summary>
        /// 记一次测量：直方图计数并落桶，计数器和 UpDownCounter 累加，可观测的仪器直接覆盖。
        /// </summary>
        /// <param name="value">测量值。</param>
        /// <param name="tags">维度。</param>
        public void Record(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var labels = Labels(tags);
            var item = series.GetOrAdd(labels, _ => new Series(buckets.Length));
            lock (item)
            {
                switch (Kind)
                {
                    case Kind.Histogram:
                        // 二分查找落在哪个桶：找到边界值时算在这个桶（le 含等于），找不到时取插入位置。
                        item.Count++;
                        item.Value += value;
                        var index = Array.BinarySearch(buckets, value);
                        item.Buckets[index >= 0 ? index : ~index]++;
                        break;
                    case Kind.Counter when !observable:
                        item.Value += value;
                        break;
                    case Kind.Gauge when !observable:
                        item.Value += value;
                        break;
                    default:
                        // 可观测的计数器报告累计值，可观测的仪表报告当前值：都是直接覆盖。
                        item.Value = value;
                        break;
                }
            }
        }

        /// <summary>
        /// 把这个指标写成 Prometheus 文本：# HELP、# TYPE，然后每个序列一行；直方图写累计分桶、_sum、_count。还没有任何测量时不写。
        /// </summary>
        /// <param name="text">输出。</param>
        public void Write(StringBuilder text)
        {
            if (series.IsEmpty)
            {
                return;
            }

            var type = Kind switch
            {
                Kind.Counter => "counter",
                Kind.Histogram => "histogram",
                _ => "gauge",
            };
            text.Append("# HELP ").Append(Name).Append(' ').Append(help).Append('\n');
            text.Append("# TYPE ").Append(Name).Append(' ').Append(type).Append('\n');
            foreach (var (labels, item) in series.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                lock (item)
                {
                    if (Kind != Kind.Histogram)
                    {
                        text.Append(Name).Append(Braces(labels)).Append(' ').Append(Number(item.Value)).Append('\n');
                        continue;
                    }

                    // Prometheus 的分桶是累计的：le="x" 表示不超过 x 的个数，最后一个 +Inf 等于总数。
                    long cumulative = 0;
                    for (var i = 0; i <= buckets.Length; i++)
                    {
                        cumulative += item.Buckets[i];
                        var le = i < buckets.Length ? Number(buckets[i]) : "+Inf";
                        var withLe = labels.Length == 0 ? $"le=\"{le}\"" : $"{labels},le=\"{le}\"";
                        text.Append(Name).Append("_bucket{").Append(withLe).Append("} ").Append(cumulative.ToString(CultureInfo.InvariantCulture)).Append('\n');
                    }

                    text.Append(Name).Append("_sum").Append(Braces(labels)).Append(' ').Append(Number(item.Value)).Append('\n');
                    text.Append(Name).Append("_count").Append(Braces(labels)).Append(' ').Append(item.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
            }
        }

        /// <summary>
        /// 维度加上大括号；没有维度时为空。
        /// </summary>
        /// <param name="labels">a="1",b="2"。</param>
        /// <returns>{a="1",b="2"}，或空字符串。</returns>
        private static string Braces(string labels) => labels.Length == 0 ? "" : "{" + labels + "}";

        /// <summary>
        /// 维度按名字排序拼成 <c>a="1",b="2"</c>，既是序列的键，也是输出的标签。
        /// </summary>
        /// <param name="tags">测量时带的维度。</param>
        /// <returns>拼好的维度；没有维度时为空字符串。</returns>
        private static string Labels(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (tags.Length == 0)
            {
                return "";
            }

            // 维度顺序不固定，排序后同一组维度才能落到同一个序列。
            var pairs = new KeyValuePair<string, object?>[tags.Length];
            tags.CopyTo(pairs);
            Array.Sort(pairs, (a, b) => string.CompareOrdinal(a.Key, b.Key));
            var text = new StringBuilder();
            foreach (var (key, value) in pairs)
            {
                if (text.Length > 0)
                {
                    text.Append(',');
                }

                var formatted = value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";
                text.Append(Sanitize(key)).Append("=\"").Append(Escape(formatted)).Append('"');
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// 一个序列的累计值。直方图多一组分桶计数（最后一个是超过最大边界的）。
    /// </summary>
    /// <param name="buckets">分桶边界的个数。</param>
    private sealed class Series(int buckets)
    {
        /// <summary>
        /// 计数器、仪表的值；直方图的总和。
        /// </summary>
        public double Value;

        /// <summary>
        /// 直方图的测量次数。
        /// </summary>
        public long Count;

        /// <summary>
        /// 直方图每个桶的计数（不累计），最后一个是超过最大边界的。
        /// </summary>
        public readonly long[] Buckets = new long[buckets + 1];
    }
}
