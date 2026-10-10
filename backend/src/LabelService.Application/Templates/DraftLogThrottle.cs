using System.Collections.Concurrent;

namespace LabelService.Application.Templates;

/// <summary>
/// 保存草稿的操作日志节流：设计器每隔几秒自动保存一次，同一个人同一个模板 10 分钟只记一条。
/// </summary>
/// <remarks>
/// 单例，只在内存里记，服务重启后从头算（重启后多记一条没有关系）。
/// </remarks>
public sealed class DraftLogThrottle
{
    /// <summary>
    /// 同一个人同一个模板两条日志的最小间隔。
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 上次记日志的时间：(用户 ID, 模板编码) → UTC 时间。
    /// </summary>
    private readonly ConcurrentDictionary<(int User, string Code), DateTime> lastLogged = new();

    /// <summary>
    /// 这次保存要不要记操作日志；要记时同时登记这次的时间。
    /// </summary>
    /// <param name="userId">操作人 ID。</param>
    /// <param name="code">模板编码。</param>
    /// <param name="now">当前时间（UTC）。</param>
    /// <returns>距上次记录超过 <see cref="Interval"/>（或从没记过）时为 true。</returns>
    public bool ShouldLog(int userId, string code, DateTime now)
    {
        if (lastLogged.TryGetValue((userId, code), out var last) && now - last <= Interval)
        {
            return false;
        }

        lastLogged[(userId, code)] = now;
        return true;
    }
}
