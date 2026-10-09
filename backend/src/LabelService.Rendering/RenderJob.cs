using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Rendering;

/// <summary>
/// 一次渲染任务：一个模板版本、一张或多张标签的数据、输出参数。参数已由调用方（服务端接口）校验过。
/// </summary>
public sealed class RenderJob
{
    /// <summary>
    /// 已发布的模板内容。渲染期间只读，不能修改。
    /// </summary>
    public required LabelTemplate Template
    {
        get; init;
    }

    /// <summary>
    /// 每张标签的数据和份数，输出顺序与此一致。
    /// </summary>
    public required IReadOnlyList<LabelInstance> Labels
    {
        get; init;
    }

    /// <summary>
    /// 输出格式。
    /// </summary>
    public required LabelFormat Format
    {
        get; init;
    }

    /// <summary>
    /// 实际使用的分辨率（请求 → 模板 → 300）。
    /// </summary>
    public required int Dpi
    {
        get; init;
    }

    /// <summary>
    /// 仅 PNG 有效：输出黑白图。
    /// </summary>
    public bool Mono
    {
        get; init;
    }

    /// <summary>
    /// 拼版版式。为 null 时一页一张标签（原来的行为）；不为 null 时按格子顺序排到纸上，只支持 PDF 和 PNG。
    /// 调用方已校验模板尺寸和格子尺寸一致。
    /// </summary>
    public SheetLayout? Layout
    {
        get; init;
    }

    /// <summary>
    /// 用了版式时第一页从第几个格子开始，1 到格子数，默认 1。
    /// </summary>
    public int StartSlot { get; init; } = 1;

    /// <summary>
    /// 拼版时不随数据变化的元素是否只排版、绘制一次，各格子回放。默认开启，基准测试关掉它做对比。
    /// </summary>
    internal bool ShareStaticElements { get; init; } = true;

    /// <summary>
    /// 计时对象。服务端传入已记录 auth、tpl、validate 的实例，渲染引擎接着记录 layout、draw、encode。
    /// </summary>
    public RenderTimings Timings { get; init; } = new();
}

/// <summary>
/// 一张标签：校验后的字段值和份数。
/// </summary>
/// <param name="Values">由 <see cref="Validation.FieldValidator"/> 转换后的值。</param>
/// <param name="Copies">份数，1–100。</param>
public sealed record LabelInstance(IReadOnlyDictionary<string, object?> Values, int Copies = 1);
