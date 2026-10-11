using LabelService.Application.Resources.Dtos;
using LabelService.Application.SystemSettings;
using LabelService.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure.Storage;

/// <summary>
/// 公司 Logo 文件存放的实现：数据目录的 branding/ 下。
/// </summary>
/// <param name="data">数据目录。</param>
internal sealed class BrandingFileStore(IOptions<DataOptions> data) : IBrandingFileStore
{
    /// <summary>
    /// 数据目录下放 Logo 的子目录名。
    /// </summary>
    public const string Folder = "branding";

    /// <summary>
    /// 支持的扩展名和对应的 MIME 类型。
    /// </summary>
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".svg"] = "image/svg+xml",
    };

    /// <summary>
    /// Logo 目录的绝对路径。
    /// </summary>
    private string LogoDirectory => Path.Combine(data.Value.ResolveDirectory(), Folder);

    /// <inheritdoc />
    public async Task SaveAsync(string fileName, byte[] content)
    {
        Directory.CreateDirectory(LogoDirectory);
        await File.WriteAllBytesAsync(PathOf(fileName) ?? throw new ArgumentException("文件名不合法", nameof(fileName)), content);
    }

    /// <inheritdoc />
    public void Delete(string fileName)
    {
        if (PathOf(fileName) is { } path && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <inheritdoc />
    public AssetFileDto? Find(string fileName) =>
        PathOf(fileName) is { } path && File.Exists(path)
            ? new AssetFileDto(File.ReadAllBytes(path), Types[Path.GetExtension(path)], File.GetLastWriteTimeUtc(path))
            : null;

    /// <summary>
    /// 文件的绝对路径。文件名来自数据库，这里再确认一次只含字母、数字、连字符和支持的扩展名，不会跑出 branding/ 目录。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <returns>绝对路径；文件名不合法时为 null。</returns>
    private string? PathOf(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var safe = stem.Length is > 0 and <= 64
            && stem.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-')
            && Types.ContainsKey(Path.GetExtension(fileName))
            && fileName == stem + Path.GetExtension(fileName);
        return safe ? Path.Combine(LogoDirectory, fileName) : null;
    }
}
