using LabelService.Application.Resources;
using LabelService.Application.Resources.Dtos;
using LabelService.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure.Storage;

/// <summary>
/// 素材文件存放的实现：上传的放在数据目录的 assets/，内置的在程序目录的 images/。
/// </summary>
/// <param name="data">数据目录。</param>
/// <param name="assets">内置素材目录。</param>
internal sealed class AssetFileStore(IOptions<DataOptions> data, IOptions<AssetOptions> assets) : IAssetFileStore
{
    /// <summary>
    /// 数据目录下放上传素材的子目录名。
    /// </summary>
    public const string UploadFolder = "assets";

    /// <summary>
    /// 支持的扩展名和对应的 MIME 类型，按查找顺序排列。
    /// </summary>
    private static readonly (string Extension, string ContentType)[] Types =
    [
        (".png", "image/png"),
        (".jpg", "image/jpeg"),
        (".svg", "image/svg+xml"),
    ];

    /// <summary>
    /// 上传素材目录的绝对路径。
    /// </summary>
    private string UploadDirectory => Path.Combine(data.Value.ResolveDirectory(), UploadFolder);

    /// <inheritdoc />
    public async Task SaveAsync(string fileName, byte[] content)
    {
        Directory.CreateDirectory(UploadDirectory);
        await File.WriteAllBytesAsync(Path.Combine(UploadDirectory, fileName), content);
    }

    /// <inheritdoc />
    public void Delete(string fileName)
    {
        var path = Path.Combine(UploadDirectory, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <inheritdoc />
    public AssetFileDto? Find(string key)
    {
        // 上传的优先于内置的（同名时上传的覆盖内置的）。
        foreach (var directory in new[] { UploadDirectory, assets.Value.ResolveImageDirectory() })
        {
            foreach (var (extension, contentType) in Types)
            {
                var path = Path.Combine(directory, key + extension);
                if (File.Exists(path))
                {
                    return new AssetFileDto(File.ReadAllBytes(path), contentType, File.GetLastWriteTimeUtc(path));
                }
            }
        }

        return null;
    }
}
