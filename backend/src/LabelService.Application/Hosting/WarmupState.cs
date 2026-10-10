namespace LabelService.Application.Hosting;

/// <summary>
/// 预热状态，接口层的就绪检查（/health/ready）读它。
/// </summary>
public sealed class WarmupState
{
    /// <summary>
    /// 是否已经预热完成。跨线程读写，用 volatile。
    /// </summary>
    private volatile bool ready;

    /// <summary>
    /// 是否已经预热完成。
    /// </summary>
    public bool Ready => ready;

    /// <summary>
    /// 预热用时。
    /// </summary>
    public TimeSpan Duration
    {
        get; private set;
    }

    /// <summary>
    /// 预热了几个模板。
    /// </summary>
    public int Templates
    {
        get; private set;
    }

    /// <summary>
    /// 标记预热完成。
    /// </summary>
    /// <param name="duration">预热用时。</param>
    /// <param name="templates">预热了几个模板。</param>
    public void MarkReady(TimeSpan duration, int templates)
    {
        Duration = duration;
        Templates = templates;
        ready = true;
    }
}
