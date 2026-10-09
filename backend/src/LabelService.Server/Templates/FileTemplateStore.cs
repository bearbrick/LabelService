using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Templates;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Templates;

/// <summary>
/// 文件模板存储：启动时把目录里的模板 JSON 全部读进内存，每个文件算作版本 1 的已发布模板。
/// </summary>
/// <remarks>
/// 默认用数据库（<see cref="DbTemplateStore"/>）；配置 LabelService:Templates:Source = Files 时用它，
/// 适合没有数据目录的场合，如只读演示环境。开发环境首次建库时也用 <see cref="ReadDirectory"/> 导入示例模板。
/// </remarks>
internal sealed class FileTemplateStore : ITemplateStore
{
    private readonly Dictionary<string, PublishedTemplate> templates = new(StringComparer.Ordinal);

    /// <summary>
    /// 加载模板目录。目录不存在时以空存储启动并记录警告。
    /// </summary>
    public FileTemplateStore(IOptions<TemplateStoreOptions> options, ILogger<FileTemplateStore> logger)
    {
        var directory = options.Value.ResolveSeedDirectory();
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("模板目录 {Directory} 不存在，没有可用模板", directory);
            return;
        }

        foreach (var template in ReadDirectory(directory))
        {
            templates.Add(template.Code, new PublishedTemplate(template, 1));
        }

        logger.LogInformation("已加载 {Count} 个模板：{Codes}", templates.Count, string.Join(", ", templates.Keys));
    }

    /// <summary>
    /// 读取目录里的全部模板 JSON，按文件名排序。
    /// </summary>
    public static IEnumerable<LabelTemplate> ReadDirectory(string directory) =>
        Directory.EnumerateFiles(directory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<LabelTemplate>(File.ReadAllText(path), LabelJson.Options)
                ?? throw new InvalidDataException($"模板文件为空：{path}"));

    /// <inheritdoc />
    public bool TryGetPublished(string templateCode, int? version, [NotNullWhen(true)] out PublishedTemplate? template)
    {
        if (templates.TryGetValue(templateCode, out template) && (version is null || version == template.Version))
        {
            return true;
        }

        template = null;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<PublishedTemplate> LatestPublished() => templates.Values.ToArray();
}

/// <summary>
/// 配置节 LabelService:Templates。
/// </summary>
internal sealed class TemplateStoreOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Templates";

    /// <summary>
    /// 模板来源，默认数据库。
    /// </summary>
    public TemplateSource Source { get; set; } = TemplateSource.Database;

    /// <summary>
    /// 模板 JSON 目录，相对于程序输出目录。来源是 Files 时从这里加载；开发环境首次建库时从这里导入示例模板。
    /// </summary>
    public string SeedDirectory { get; set; } = "SeedTemplates";

    /// <summary>
    /// 模板 JSON 目录的绝对路径。
    /// </summary>
    public string ResolveSeedDirectory() => Path.Combine(AppContext.BaseDirectory, SeedDirectory);
}

/// <summary>
/// 模板来源。
/// </summary>
internal enum TemplateSource
{
    /// <summary>
    /// 数据库（SQLite），支持草稿、版本发布。
    /// </summary>
    Database,

    /// <summary>
    /// 模板 JSON 目录，只读，版本号固定为 1。
    /// </summary>
    Files,
}
