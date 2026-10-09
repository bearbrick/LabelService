namespace LabelService.Rendering.Diagnostics;

/// <summary>
/// 一个缓存的命中和未命中次数（进程启动以来）。服务端据此导出 label_cache_hit_ratio（T-010）。
/// </summary>
/// <remarks>
/// 只做 Interlocked 加一，热路径上没有分配、没有锁。
/// </remarks>
public sealed class CacheCounter
{
    private long hits;
    private long misses;

    /// <summary>命中次数。</summary>
    public long Hits => Interlocked.Read(ref hits);

    /// <summary>未命中次数。</summary>
    public long Misses => Interlocked.Read(ref misses);

    /// <summary>
    /// 命中率；还没有访问过时返回 null。
    /// </summary>
    public double? HitRatio
    {
        get
        {
            var hit = Hits;
            var total = hit + Misses;
            return total == 0 ? null : (double)hit / total;
        }
    }

    /// <summary>记一次命中。</summary>
    public void Hit() => Interlocked.Increment(ref hits);

    /// <summary>记一次未命中。</summary>
    public void Miss() => Interlocked.Increment(ref misses);
}

/// <summary>
/// 渲染引擎里的缓存：条码编码结果、按尺寸缩放的图片素材、字体（未命中 = 降级到默认字体）。
/// </summary>
public static class RenderCaches
{
    /// <summary>条码、二维码的编码结果（按码制、参数、内容）。</summary>
    public static CacheCounter Symbols { get; } = new();

    /// <summary>图片素材按目标尺寸栅格化的结果。</summary>
    public static CacheCounter Images { get; } = new();

    /// <summary>字体：模板字体名找到了对应字体算命中，降级到默认字体算未命中。</summary>
    public static CacheCounter Fonts { get; } = new();
}
