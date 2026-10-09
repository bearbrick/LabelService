using System.Text.RegularExpressions;

namespace LabelService.Client.Checks;

/// <summary>
/// 调用端的约定：SDK 不引用其他项目、能编译到 net462、强制 XML 注释、文件规模。规则说明见根目录 AGENTS.md 的 R4。
/// </summary>
internal static partial class ArchitectureChecks
{
    public static void Run(string clientRoot, Action<string, bool> check)
    {
        string Relative(string path) => Path.GetRelativePath(clientRoot, path).Replace('\\', '/');

        var sdkProject = File.ReadAllText(Path.Combine(clientRoot, "src", "LabelService.Client", "LabelService.Client.csproj"));
        check("SDK 不引用任何项目", !sdkProject.Contains("<ProjectReference"));
        check("SDK 的目标框架是 net462、netstandard2.0、net8.0", sdkProject.Contains("<TargetFrameworks>net462;netstandard2.0;net8.0</TargetFrameworks>"));

        var props = File.ReadAllText(Path.Combine(clientRoot, "src", "Directory.Build.props"));
        check("SDK 强制公开成员写 XML 注释", props.Contains("<GenerateDocumentationFile>true</GenerateDocumentationFile>") && props.Contains("CS1591"));

        var separator = Path.DirectorySeparatorChar;
        var sources = Directory.GetFiles(Path.Combine(clientRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}bin{separator}") && !path.Contains($"{separator}obj{separator}"))
            .ToArray();
        var large = sources.Where(path => File.ReadLines(path).Count() >= 500).Select(Relative).ToArray();
        check("SDK 源码文件都少于 500 行" + (large.Length > 0 ? "：" + string.Join(", ", large) : ""), large.Length == 0);
        var modern = sources.Where(path => NewRuntimeSyntax().IsMatch(File.ReadAllText(path))).Select(Relative).ToArray();
        check("SDK 不用 net462 不支持的 record、init、required" + (modern.Length > 0 ? "：" + string.Join(", ", modern) : ""), modern.Length == 0);
        var heavy = sources.Where(path => ForbiddenDependency().IsMatch(File.ReadAllText(path))).Select(Relative).ToArray();
        check("SDK 不依赖 Newtonsoft.Json 和日志框架" + (heavy.Length > 0 ? "：" + string.Join(", ", heavy) : ""), heavy.Length == 0 && !ForbiddenDependency().IsMatch(sdkProject));
    }

    [GeneratedRegex(@"\brecord\b|\binit\s*;|\brequired\s+[A-Za-z<]")]
    private static partial Regex NewRuntimeSyntax();

    [GeneratedRegex(@"Newtonsoft|Microsoft\.Extensions\.Logging|Serilog|NLog|log4net")]
    private static partial Regex ForbiddenDependency();
}
