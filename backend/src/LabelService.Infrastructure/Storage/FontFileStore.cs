using LabelService.Application.Resources;
using LabelService.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure.Storage;

/// <summary>
/// 上传字体文件存放的实现：数据目录的 fonts/。
/// </summary>
/// <param name="data">数据目录。</param>
internal sealed class FontFileStore(IOptions<DataOptions> data) : IFontFileStore
{
    /// <summary>
    /// 数据目录下放上传字体的子目录名。
    /// </summary>
    public const string Folder = "fonts";

    /// <summary>
    /// 上传字体目录的绝对路径。
    /// </summary>
    /// <param name="data">数据目录的配置。</param>
    /// <returns>绝对路径。</returns>
    public static string ResolveDirectory(DataOptions data) => Path.Combine(data.ResolveDirectory(), Folder);

    /// <inheritdoc />
    public async Task SaveAsync(string fileName, byte[] content)
    {
        var directory = ResolveDirectory(data.Value);
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(PathOf(directory, fileName), content);
    }

    /// <inheritdoc />
    public void Delete(string fileName)
    {
        var path = PathOf(ResolveDirectory(data.Value), fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 文件的绝对路径；再确认一次文件名不带目录，不会跑出 fonts/ 目录。
    /// </summary>
    /// <param name="directory">上传字体目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <returns>绝对路径。</returns>
    /// <exception cref="ArgumentException">文件名带了目录或是空的。</exception>
    private static string PathOf(string directory, string fileName) =>
        fileName.Length > 0 && Path.GetFileName(fileName) == fileName && fileName is not ("." or "..")
            ? Path.Combine(directory, fileName)
            : throw new ArgumentException("文件名不合法", nameof(fileName));
}
