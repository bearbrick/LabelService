using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Templates;

namespace LabelService.Backend.Checks;

/// <summary>
/// 检查共用的数据和工具。
/// </summary>
internal static class Fixtures
{
    /// <summary>开发环境的测试 Key（appsettings.Development.json 里只存它的 SHA-256）。</summary>
    public const string DevApiKey = "lbl_test_local_dev";

    /// <summary>示例箱标的完整数据。</summary>
    public const string BoxLabelData =
        """{"partName":"电源适配器","partNo":"3100-0123","qty":50,"batchNo":"20261008-A","prodDate":"2026-10-08","boxNo":"C2610080001"}""";

    /// <summary>
    /// 从输出目录逐级向上找到后端根目录（有 LabelService.Backend.slnx 的目录）。
    /// </summary>
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
    public static LabelTemplate LoadTemplate(string backendRoot, string fileName = "box-label.json") =>
        JsonSerializer.Deserialize<LabelTemplate>(File.ReadAllText(Path.Combine(backendRoot, "samples", "templates", fileName)), LabelJson.Options)!;

    /// <summary>
    /// 把 JSON 对象文本转成接口收到的字段数据。
    /// </summary>
    public static Dictionary<string, JsonElement> Data(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
}
