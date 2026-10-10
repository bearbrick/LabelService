using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Templates;

namespace LabelService.Infrastructure.Storage;

/// <summary>
/// 读取目录里的模板 JSON、版式 JSON（模板来源是文件时加载、开发环境导入示例时用）。
/// </summary>
public static class SeedFiles
{
    /// <summary>
    /// 读取目录里的全部模板 JSON，按文件名排序。
    /// </summary>
    /// <param name="directory">目录。</param>
    /// <returns>模板，按需逐个读取。</returns>
    /// <exception cref="InvalidDataException">文件内容为空。</exception>
    /// <exception cref="JsonException">文件不是合法的模板 JSON。</exception>
    public static IEnumerable<LabelTemplate> ReadTemplates(string directory) => Read<LabelTemplate>(directory, "模板");

    /// <summary>
    /// 读取目录里的全部版式 JSON，按文件名排序。
    /// </summary>
    /// <param name="directory">目录。</param>
    /// <returns>版式，按需逐个读取。</returns>
    /// <exception cref="InvalidDataException">文件内容为空。</exception>
    /// <exception cref="JsonException">文件不是合法的版式 JSON。</exception>
    public static IEnumerable<SheetLayout> ReadLayouts(string directory) => Read<SheetLayout>(directory, "版式");

    /// <summary>
    /// 读取目录里的全部 JSON 文件。
    /// </summary>
    /// <typeparam name="T">内容类型。</typeparam>
    /// <param name="directory">目录。</param>
    /// <param name="kind">内容的中文名，报错用。</param>
    /// <returns>内容，按文件名排序。</returns>
    private static IEnumerable<T> Read<T>(string directory, string kind)
        where T : class =>
        Directory.EnumerateFiles(directory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<T>(File.ReadAllText(path), LabelJson.Options)
                ?? throw new InvalidDataException($"{kind}文件为空：{path}"));
}
