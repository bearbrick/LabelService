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

    private static readonly double[] MillisecondBuckets = [1, 2, 5, 10, 20, 30, 50, 75, 100, 150, 200, 300, 500, 1000, 2000, 5000];
    private static readonly double[] SecondBuckets = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];

    private readonly MeterListener listener = new();
    private readonly ConcurrentDictionary<Instrument, Family> families = new();
    private readonly Lock scrape = new();

    /// <summary>
    /// 开始监听。<paramref name="meterFactory"/> 是本主机的 Meter 工厂，用来区分同一进程里其他主机的指标。
    /// </summary>
    public PrometheusExporter(IMeterFactory meterFactory)
    {
        listener.InstrumentPublished = (instrument, meters) =>
        {
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
    /// 生成 Prometheus 文本格式（0.0.4）。可观测的仪表在这时读一次当前值。
    /// </summary>
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
    /// Prometheus 的指标名。
    /// </summary>
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

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Number(double value) =>
        double.IsPositiveInfinity(value) ? "+Inf" : double.IsNegativeInfinity(value) ? "-Inf" : value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>指标类型。</summary>
    internal enum Kind
    {
        /// <summary>只增的计数器（Counter、ObservableCounter）。</summary>
        Counter,

        /// <summary>当前值（UpDownCounter、Gauge 和对应的可观测版本）。</summary>
        Gauge,

        /// <summary>直方图。</summary>
        Histogram,
    }

    /// <summary>
    /// 一个指标的全部序列（不同维度组合）。
    /// </summary>
    private sealed class Family
    {
        private readonly ConcurrentDictionary<string, Series> series = new(StringComparer.Ordinal);
        private readonly bool observable;
        private readonly double[] buckets;
        private readonly string help;

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

        public string Name
        {
            get;
        }

        public Kind Kind
        {
            get;
        }

        public void Record(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var labels = Labels(tags);
            var item = series.GetOrAdd(labels, _ => new Series(buckets.Length));
            lock (item)
            {
                switch (Kind)
                {
                    case Kind.Histogram:
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

        private static string Braces(string labels) => labels.Length == 0 ? "" : "{" + labels + "}";

        /// <summary>
        /// 维度按名字排序拼成 <c>a="1",b="2"</c>，既是序列的键，也是输出的标签。
        /// </summary>
        private static string Labels(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (tags.Length == 0)
            {
                return "";
            }

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
    private sealed class Series(int buckets)
    {
        public double Value;
        public long Count;
        public readonly long[] Buckets = new long[buckets + 1];
    }
}
