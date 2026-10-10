using LabelService.Contracts.Protocol;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Application.OpenApi;

/// <summary>
/// 开放接口一次调用的过程记录：各阶段耗时，以及处理过程中逐步确定的模板、版本、格式、版式、DPI。
/// </summary>
/// <remarks>
/// 接口层创建（从收到请求开始计时），应用服务边处理边填写；请求结束后接口层据此写指标、日志和调用日志。
/// 处理失败时已经填写的部分照样有用（如模板编码），所以不放在返回值里。
/// </remarks>
public sealed class RenderTrace
{
    /// <summary>
    /// 从请求开始计时。
    /// </summary>
    /// <param name="started">收到请求时的 <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>。</param>
    public RenderTrace(long started)
    {
        Started = started;
        Checkpoint = started;
    }

    /// <summary>
    /// 收到请求时的时间戳。
    /// </summary>
    public long Started
    {
        get;
    }

    /// <summary>
    /// 上一个阶段结束时的时间戳；下一个阶段从这里算起。
    /// </summary>
    public long Checkpoint
    {
        get;
        private set;
    }

    /// <summary>
    /// 分阶段耗时（auth、tpl、validate、layout、draw、encode、total）。
    /// </summary>
    public RenderTimings Timings { get; } = new();

    /// <summary>
    /// 模板编码；请求里还没读到时为空字符串。
    /// </summary>
    public string TemplateCode { get; set; } = "";

    /// <summary>
    /// 实际使用的模板版本；还没找到模板时为 null。
    /// </summary>
    public int? TemplateVersion
    {
        get;
        set;
    }

    /// <summary>
    /// 输出格式；还没解析时为 null。
    /// </summary>
    public LabelFormat? Format
    {
        get;
        set;
    }

    /// <summary>
    /// 拼版版式编码（最长保留 64 个字符）；不拼版时为 null。
    /// </summary>
    public string? LayoutCode
    {
        get;
        set;
    }

    /// <summary>
    /// 实际使用的 DPI；还没确定时为 0。
    /// </summary>
    public int Dpi
    {
        get;
        set;
    }

    /// <summary>
    /// 记一个阶段：从上一个阶段结束到现在的耗时累加到 <paramref name="stage"/>。
    /// </summary>
    /// <param name="stage">阶段名，见 <see cref="TimingStages"/>。</param>
    public void Mark(string stage) => Checkpoint = Timings.Mark(stage, Checkpoint);
}
