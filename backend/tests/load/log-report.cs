#:property ManagePackageVersionsCentrally=false
#:package Microsoft.Data.Sqlite.Core@10.0.12
#:package SQLitePCLRaw.bundle_e_sqlite3@2.1.12
// 从调用日志库 label-log.db 算单张接口在一个时间窗内的耗时分位数。duration_ms 从开始处理请求算到写完最后一个字节，
// 是设计书“服务端处理时间”的口径；响应头 Server-Timing 的 total 只到写响应头为止，偏小。包版本和服务端用的一致。
// 用法（压测前记下 UTC 时间）：dotnet run log-report.cs <label-log.db> <起始 UTC，如 2026-10-10T07:20:00Z> [结束 UTC]
using System.Globalization;
using Microsoft.Data.Sqlite;

if (args.Length < 2)
{
    Console.Error.WriteLine("用法：dotnet run log-report.cs <label-log.db> <起始 UTC> [结束 UTC]");
    return 2;
}

SQLitePCL.Batteries_V2.Init();
var styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
var from = DateTime.Parse(args[1], CultureInfo.InvariantCulture, styles);
var to = args.Length > 2 ? DateTime.Parse(args[2], CultureInfo.InvariantCulture, styles) : DateTime.MaxValue;
using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = args[0], Mode = SqliteOpenMode.ReadOnly }.ToString());
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = "SELECT duration_ms, status, created_at FROM render_log WHERE endpoint = 'render' ORDER BY id";
var durations = new List<double>();
var statuses = new SortedDictionary<string, int>(StringComparer.Ordinal);
using (var reader = command.ExecuteReader())
{
    while (reader.Read())
    {
        var created = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, styles);
        if (created < from || created > to)
        {
            continue;
        }

        var status = reader.GetString(1);
        statuses[status] = statuses.GetValueOrDefault(status) + 1;
        if (status == "ok")
        {
            durations.Add(reader.GetDouble(0));
        }
    }
}

durations.Sort();

// 最近秩法，和管理后台“今日单张 P95”一致。
double Percentile(double p) => durations[Math.Clamp((int)Math.Ceiling(p * durations.Count) - 1, 0, durations.Count - 1)];

Console.WriteLine($"单张接口：成功 {durations.Count} 次；结果 {string.Join("、", statuses.Select(item => $"{item.Key} {item.Value}"))}");
if (durations.Count > 0)
{
    Console.WriteLine($"duration_ms（到写完最后一个字节）：P50 {Percentile(0.50):0.0}，P95 {Percentile(0.95):0.0}，P99 {Percentile(0.99):0.0}，最大 {durations[^1]:0.0}");
}

return 0;
