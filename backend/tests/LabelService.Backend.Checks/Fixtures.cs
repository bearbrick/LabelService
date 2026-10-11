using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Fonts;

namespace LabelService.Backend.Checks;

/// <summary>
/// 检查共用的数据和工具。
/// </summary>
internal static class Fixtures
{
    /// <summary>
    /// 开发环境的测试 Key（appsettings.Development.json 里只存它的 SHA-256）。
    /// </summary>
    public const string DevApiKey = "lbl_test_local_dev";

    /// <summary>
    /// 示例箱标的完整数据。
    /// </summary>
    public const string BoxLabelData =
        """{"partName":"电源适配器","partNo":"3100-0123","qty":50,"batchNo":"20261008-A","prodDate":"2026-10-08","boxNo":"C2610080001"}""";

    /// <summary>
    /// 加载过的字体（整个检查程序共用一份，第一次用到时加载）。
    /// </summary>
    private static FontRegistry? fonts;

    /// <summary>
    /// 加载过的素材（整个检查程序共用一份，第一次用到时加载）。
    /// </summary>
    private static AssetLibrary? assets;

    /// <summary>
    /// 从输出目录逐级向上找到后端根目录（有 LabelService.Backend.slnx 的目录）。
    /// </summary>
    /// <returns>backend 目录的完整路径。</returns>
    /// <exception cref="DirectoryNotFoundException">不在仓库里运行。</exception>
    public static string BackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "LabelService.Backend.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("未找到 LabelService.Backend.slnx，请在仓库内运行检查程序。");
    }

    /// <summary>
    /// 读取 backend/samples/templates 下的示例模板。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="fileName">模板文件名，默认示例箱标。</param>
    /// <returns>模板（每次读一份新的，可以随便改）。</returns>
    public static LabelTemplate LoadTemplate(string backendRoot, string fileName = "box-label.json") =>
        JsonSerializer.Deserialize<LabelTemplate>(File.ReadAllText(Path.Combine(backendRoot, "samples", "templates", fileName)), LabelJson.Options)!;

    /// <summary>
    /// backend/assets/fonts 里的字体，整个检查程序共用一份。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <returns>字体注册表。</returns>
    public static FontRegistry Fonts(string backendRoot) =>
        fonts ??= FontRegistry.Load(Path.Combine(backendRoot, "assets", "fonts"));

    /// <summary>
    /// backend/assets 里的图标和内置图片素材，整个检查程序共用一份。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <returns>素材库。</returns>
    public static AssetLibrary Assets(string backendRoot) =>
        assets ??= AssetLibrary.Load(Path.Combine(backendRoot, "assets", "icons"), [Path.Combine(backendRoot, "assets", "images")]);

    /// <summary>
    /// 带字体和素材的渲染引擎，和服务端一致。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <returns>渲染引擎。</returns>
    public static LabelRenderer Renderer(string backendRoot) => new(Fonts(backendRoot), Assets(backendRoot));

    /// <summary>
    /// 把 JSON 对象文本转成接口收到的字段数据。
    /// </summary>
    /// <param name="json">JSON 对象，如 <see cref="BoxLabelData"/>。</param>
    /// <returns>字段 key → 值。</returns>
    public static Dictionary<string, JsonElement> Data(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
}
