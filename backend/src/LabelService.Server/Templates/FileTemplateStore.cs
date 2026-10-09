using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Templates;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Templates;

/// <summary>
/// 临时的模板存储：启动时把目录里的模板 JSON 全部读进内存，每个文件算作版本 1 的已发布模板。
/// </summary>
/// <remarks>
/// 模板数据库、草稿和版本发布完成后（T-008）由数据库实现替换，接口不变。
/// </remarks>
internal sealed class FileTemplateStore : ITemplateStore
{
    private readonly Dictionary<string, PublishedTemplate> templates = new(StringComparer.Ordinal);

    /// <summary>
    /// 加载模板目录。目录不存在时以空存储启动并记录警告。
    /// </summary>
    public FileTemplateStore(IOptions<TemplateStoreOptions> options, ILogger<FileTemplateStore> logger)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, options.Value.SeedDirectory);
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("模板目录 {Directory} 不存在，没有可用模板", directory);
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var template = JsonSerializer.Deserialize<LabelTemplate>(File.ReadAllText(path), LabelJson.Options)
                ?? throw new InvalidDataException($"模板文件为空：{path}");
            templates.Add(template.Code, new PublishedTemplate(template, 1));
        }

        logger.LogInformation("已加载 {Count} 个模板：{Codes}", templates.Count, string.Join(", ", templates.Keys));
    }

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
    /// 模板 JSON 目录，相对于程序输出目录。
    /// </summary>
    public string SeedDirectory { get; set; } = "SeedTemplates";
}
