using System.Security.Cryptography;
using System.Text;
using System.Xml;
using LabelService.Application.Logs;
using LabelService.Application.Resources.Dtos;
using LabelService.Application.Shared;
using LabelService.Contracts.Protocol;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Rendering.Assets;
using SkiaSharp;

namespace LabelService.Application.Resources;

/// <summary>
/// 图片素材的应用服务实现。
/// </summary>
/// <param name="assets">素材仓储。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="files">素材文件的存放。</param>
/// <param name="library">渲染用的素材库，上传、删除后重新加载。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="clock">时间来源。</param>
public sealed class AssetAppService(
    IAssetRepository assets,
    IUnitOfWork unitOfWork,
    IAssetFileStore files,
    AssetLibrary library,
    IOperationLogger operationLog,
    TimeProvider clock) : IAssetAppService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssetDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var uploaded = await assets.ListAsync(cancellationToken);
        var keys = uploaded.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);

        // 素材库里的图片包括上传的；去掉上传的，剩下的是内置素材。
        var builtin = library.ImageIds.Where(id => !keys.Contains(id)).Order(StringComparer.Ordinal)
            .Select(id => new AssetDto(id, id, true, 0, null));
        return builtin.Concat(uploaded.Select(ToDto)).ToList();
    }

    /// <inheritdoc />
    public AssetFileDto GetFile(string key) =>
        IsSafeKey(key) && files.Find(key) is { } file ? file : throw AdminErrors.NotFoundError("素材不存在");

    /// <inheritdoc />
    public async Task<AssetDto> UploadAsync(UploadAssetInput? input)
    {
        if (input is null || input.Length == 0)
        {
            throw new UserFriendlyException("请选择图片文件");
        }

        if (input.Length > RenderLimits.MaxImageBytes)
        {
            throw new UserFriendlyException("图片超过 1 MB，请压缩后再上传");
        }

        using var buffer = new MemoryStream();
        await input.Content.CopyToAsync(buffer);
        var (bytes, extension) = Identify(buffer.ToArray());
        var key = "a" + RandomNumberGenerator.GetHexString(10, lowercase: true);
        await files.SaveAsync(key + extension, bytes);
        var name = Path.GetFileNameWithoutExtension(input.FileName);
        var entity = new AssetEntity
        {
            Key = key,
            Kind = "image",
            Name = string.IsNullOrWhiteSpace(name) ? key : name.Length > 128 ? name[..128] : name,
            FilePath = key + extension,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Size = bytes.Length,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        assets.Add(entity);
        await unitOfWork.SaveChangesAsync();

        // 重新加载素材库，模板里写 asset:Key 立即可用。
        library.ReloadImages();
        await operationLog.WriteAsync("素材", entity.Name, $"上传 {extension[1..].ToUpperInvariant()}，{bytes.Length / 1024.0:0.#} KB，模板里写 asset:{key}");
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task RenameAsync(string key, RenameAssetDto input)
    {
        var name = (input.Name ?? "").Trim();
        if (name.Length is 0 or > 128)
        {
            throw new UserFriendlyException("名称不能为空，最长 128 字");
        }

        var asset = await assets.FindByKeyAsync(key) ?? throw AdminErrors.NotFoundError("素材不存在（内置素材不能改名）");
        var old = asset.Name;
        asset.Name = name;
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("素材", name, $"由“{old}”改名");
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key)
    {
        var asset = await assets.FindByKeyAsync(key) ?? throw AdminErrors.NotFoundError("素材不存在（内置素材不能删除）");
        assets.Remove(asset);
        await unitOfWork.SaveChangesAsync();
        files.Delete(asset.FilePath);
        library.ReloadImages();
        await operationLog.WriteAsync("素材", asset.Name, $"删除（引用 asset:{key} 的模板生成时会报 IMAGE_INVALID）");
    }

    /// <summary>
    /// 转成接口返回的素材。
    /// </summary>
    /// <param name="asset">上传的素材。</param>
    /// <returns>素材 DTO。</returns>
    private static AssetDto ToDto(AssetEntity asset) => new(asset.Key, asset.Name, false, asset.Size, asset.CreatedAt);

    /// <summary>
    /// Key 是否安全（能直接拼进文件名）：1–32 位字母、数字、下划线、连字符。
    /// </summary>
    /// <param name="key">素材 Key。</param>
    /// <returns>安全时为 true。</returns>
    private static bool IsSafeKey(string key) => key.Length is > 0 and <= 32 && key.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-');

    /// <summary>
    /// 按文件内容判断图片类型（不信扩展名）；SVG 清洗后返回清洗过的内容。
    /// </summary>
    /// <param name="bytes">上传的文件内容。</param>
    /// <returns>要保存的内容和扩展名（.png、.jpg、.svg）。</returns>
    /// <exception cref="UserFriendlyException">不是 PNG/JPG/SVG，或位图无法解码。</exception>
    private static (byte[] Bytes, string Extension) Identify(byte[] bytes)
    {
        // PNG、JPG 看文件头；都不是就当 SVG 试着清洗，清洗失败说明不是支持的格式。
        string extension;
        if (bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]))
        {
            extension = ".png";
        }
        else if (bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            extension = ".jpg";
        }
        else
        {
            try
            {
                return (Encoding.UTF8.GetBytes(SvgSanitizer.Sanitize(Encoding.UTF8.GetString(bytes))), ".svg");
            }
            catch (Exception ex) when (ex is XmlException or InvalidDataException)
            {
                throw new UserFriendlyException("只支持 PNG、JPG、SVG 图片");
            }
        }

        using var decoded = SKImage.FromEncodedData(bytes);
        return decoded is null ? throw new UserFriendlyException("图片无法解码，文件可能损坏") : (bytes, extension);
    }
}
