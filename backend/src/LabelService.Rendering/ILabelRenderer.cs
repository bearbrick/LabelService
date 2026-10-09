using LabelService.Contracts.Protocol;

namespace LabelService.Rendering;

/// <summary>
/// 渲染引擎入口。实现必须线程安全：服务端以单例方式并发调用。
/// </summary>
public interface ILabelRenderer
{
    /// <summary>
    /// 当前版本是否已经实现某种输出格式。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <returns>已实现返回 true。</returns>
    bool Supports(LabelFormat format);

    /// <summary>
    /// 渲染一个任务。
    /// </summary>
    /// <param name="job">已校验的渲染任务。</param>
    /// <returns>渲染结果。</returns>
    /// <exception cref="LabelRenderException">数据或模板内容无法渲染。</exception>
    /// <exception cref="NotSupportedException">输出格式尚未实现。</exception>
    RenderResult Render(RenderJob job);
}
