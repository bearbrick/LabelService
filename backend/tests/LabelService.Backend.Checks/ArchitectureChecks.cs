using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LabelService.Backend.Checks;

/// <summary>
/// 把仓库约定固化成检查：后端依赖方向、文件规模、XML 注释开关，三个部分互不引用，任务文件和文档齐全。
/// 规则说明见根目录 AGENTS.md。调用端自己的规则在 client/tests/LabelService.Client.Checks。
/// </summary>
internal static partial class ArchitectureChecks
{
    public static void Run(string backendRoot, string repositoryRoot, Action<string, bool> check)
    {
        string Project(string name) => File.ReadAllText(Path.Combine(backendRoot, "src", name, name + ".csproj"));
        string Relative(string path) => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

        var contracts = Project("LabelService.Contracts");
        check("Contracts 不引用任何项目和包", !contracts.Contains("<ProjectReference") && !contracts.Contains("<PackageReference"));
        var rendering = Project("LabelService.Rendering");
        check("Rendering 只引用 Contracts",
            Regex.Count(rendering, "<ProjectReference") == 1 && rendering.Contains(@"LabelService.Contracts\LabelService.Contracts.csproj"));
        check("Server 不引用调用端 SDK", !Project("LabelService.Server").Contains("LabelService.Client"));

        foreach (var part in new[] { "backend", "client" })
        {
            var partRoot = Path.Combine(repositoryRoot, part);
            var outside = SourceFiles(partRoot, "*.csproj")
                .SelectMany(project => XDocument.Load(project).Descendants("ProjectReference")
                    .Select(reference => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, (string)reference.Attribute("Include")!))))
                .Where(target => !target.StartsWith(partRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Select(Relative)
                .ToArray();
            check($"{part}/ 的项目只引用本部分的项目" + (outside.Length > 0 ? "：" + string.Join(", ", outside) : ""), outside.Length == 0);
        }

        var frontendRoot = Path.Combine(repositoryRoot, "frontend");
        var frontendOutside = SourceFiles(Path.Combine(frontendRoot, "src"), "*.*")
            .Where(path => Path.GetExtension(path) is ".ts" or ".vue")
            .SelectMany(path => ImportPath().Matches(File.ReadAllText(path))
                .Select(match => match.Groups["path"].Value)
                .Where(import => import.StartsWith('.'))
                .Select(import => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, import))))
            .Where(target => !target.StartsWith(frontendRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Select(Relative)
            .ToArray();
        check("frontend/ 不引用本部分以外的文件" + (frontendOutside.Length > 0 ? "：" + string.Join(", ", frontendOutside) : ""), frontendOutside.Length == 0);

        var props = File.ReadAllText(Path.Combine(backendRoot, "src", "Directory.Build.props"));
        check("后端产品项目强制公开成员写 XML 注释", props.Contains("<GenerateDocumentationFile>true</GenerateDocumentationFile>") && props.Contains("CS1591"));

        var sources = SourceFiles(Path.Combine(backendRoot, "src"), "*.cs").ToArray();
        var large = sources.Where(path => File.ReadLines(path).Count() >= 500).Select(Relative).ToArray();
        check("后端产品源码文件都少于 500 行" + (large.Length > 0 ? "：" + string.Join(", ", large) : ""), large.Length == 0);
        check("Rendering 不访问网络和数据库",
            sources.Where(path => Relative(path).StartsWith("backend/src/LabelService.Rendering/", StringComparison.Ordinal))
                .All(path => !ForbiddenInRendering().IsMatch(File.ReadAllText(path))));

        var taskFiles = Directory.GetFiles(Path.Combine(repositoryRoot, "docs", "tasks"), "T-*.md");
        var referenced = new[] { "backend", "client", Path.Combine("frontend", "src") }
            .SelectMany(part => SourceFiles(Path.Combine(repositoryRoot, part), "*.*"))
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".ts" or ".vue")
            .SelectMany(path => TaskId().Matches(File.ReadAllText(path)).Select(match => match.Value))
            .ToHashSet();
        var missing = referenced.Where(id => !taskFiles.Any(path => Path.GetFileName(path).StartsWith(id + "-", StringComparison.Ordinal))).Order().ToArray();
        check("代码里提到的任务编号都有任务文件" + (missing.Length > 0 ? "：" + string.Join(", ", missing) : ""), missing.Length == 0);
        var incomplete = taskFiles.Where(path =>
        {
            var text = File.ReadAllText(path);
            return !text.Contains("状态：") || !text.Contains("## 验收标准") || !text.Contains("## 交接记录");
        }).Select(Relative).ToArray();
        check("任务文件都有状态、验收标准和交接记录" + (incomplete.Length > 0 ? "：" + string.Join(", ", incomplete) : ""), incomplete.Length == 0);

        string[] documents =
        [
            "README.md", "AGENTS.md", "CLAUDE.md", "verify.ps1", "backend/README.md", "client/README.md", "frontend/README.md",
            "docs/architecture.md", "docs/contracts.md", "docs/performance.md", "docs/tasks/README.md",
        ];
        foreach (var document in documents)
        {
            check($"{document} 存在", File.Exists(Path.Combine(repositoryRoot, document)));
        }

        var agents = File.ReadAllText(Path.Combine(repositoryRoot, "AGENTS.md"));
        check("AGENTS.md 写明了统一验证脚本和三个部分各自的验证命令",
            agents.Contains("verify.ps1")
            && agents.Contains("dotnet build backend/LabelService.Backend.slnx")
            && agents.Contains("dotnet run --project backend/tests/LabelService.Backend.Checks")
            && agents.Contains("dotnet build client/LabelService.Client.slnx")
            && agents.Contains("dotnet run --project client/tests/LabelService.Client.Checks")
            && agents.Contains("npm run build"));
    }

    private static IEnumerable<string> SourceFiles(string directory, string pattern)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var separator = Path.DirectorySeparatorChar;
        return Directory.GetFiles(directory, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}bin{separator}") && !path.Contains($"{separator}obj{separator}") && !path.Contains($"{separator}node_modules{separator}"));
    }

    [GeneratedRegex(@"\b(HttpClient|DbContext|SqlConnection|WebRequest)\b")]
    private static partial Regex ForbiddenInRendering();

    [GeneratedRegex(@"\bT-\d{3}\b")]
    private static partial Regex TaskId();

    [GeneratedRegex("""(?:from|import)\s+['"](?<path>[^'"]+)['"]""")]
    private static partial Regex ImportPath();
}
