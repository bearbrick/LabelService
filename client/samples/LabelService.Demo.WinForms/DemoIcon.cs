using System.Drawing;

namespace LabelService.Demo;

/// <summary>
/// 演示程序共享的条码图标，资源随程序集部署，不需要外部图标文件。
/// </summary>
internal static class DemoIcon
{
    /// <summary>
    /// 主窗口和打印窗口共用的图标，进程生命周期内保留，调用方不要释放。
    /// </summary>
    public static Icon Value { get; } = Load();

    /// <summary>
    /// 从嵌入资源加载标题栏和任务栏的图标。
    /// </summary>
    /// <returns>包含多种尺寸的应用图标。</returns>
    private static Icon Load()
    {
        using var stream = typeof(DemoIcon).Assembly.GetManifestResourceStream("LabelService.Demo.AppIcon.ico")
            ?? throw new InvalidOperationException("客户端程序图标资源缺失。");
        return new Icon(stream, new Size(32, 32));
    }
}
