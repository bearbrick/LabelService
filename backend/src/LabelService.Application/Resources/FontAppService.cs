using System.Text;
using LabelService.Application.Logs;
using LabelService.Application.Resources.Dtos;
using LabelService.Application.Shared;
using LabelService.Domain.Shared;
using LabelService.Rendering.Fonts;

namespace LabelService.Application.Resources;

/// <summary>
/// 字体的应用服务实现：列出部署目录和上传目录里的字体，上传、删除后重新加载字体注册表，立即生效（ADR 0011）。
/// </summary>
/// <param name="fonts">渲染用的字体注册表。</param>
/// <param name="files">上传字体文件的存放。</param>
/// <param name="operationLog">操作日志。</param>
public sealed class FontAppService(FontRegistry fonts, IFontFileStore files, IOperationLogger operationLog) : IFontAppService
{
    /// <summary>
    /// 单个字体文件的大小上限：50 MB（中文字体常见十几到三十 MB）。
    /// </summary>
    public const long MaxFontBytes = 50L * 1024 * 1024;

    /// <summary>
    /// 操作日志里的类型。
    /// </summary>
    private const string LogType = "字体";

    /// <inheritdoc />
    public FontListDto List() => new(
        fonts.FamilyNames.Order(StringComparer.Ordinal).ToArray(),
        fonts.Files.Select(path => Path.GetFileName(path)).ToArray(),
        fonts.FileDetails.Select(item => new FontFileDto(Path.GetFileName(item.Path), item.Families, item.Size, item.LastModified, item.Uploaded)).ToArray(),
        fonts.TemplateNames,
        fonts.HasDefaultFamily);

    /// <inheritdoc />
    public async Task<FontListDto> UploadAsync(UploadAssetInput? input)
    {
        if (fonts.UploadDirectory is null)
        {
            throw new UserFriendlyException("这个服务没有配置上传字体的目录");
        }

        if (input is null || input.Length == 0)
        {
            throw new UserFriendlyException("请选择字体文件");
        }

        var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
        if (!FontRegistry.FileExtensions.Contains(extension))
        {
            throw new UserFriendlyException("只支持 .ttf、.otf、.ttc、.otc 字体文件");
        }

        if (input.Length > MaxFontBytes)
        {
            throw new UserFriendlyException("字体文件超过 50 MB");
        }

        var fileName = SafeFileName(input.FileName, extension);
        if (fonts.Files.Any(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UserFriendlyException($"已经有同名的字体文件 {fileName}，先删除或改个文件名再上传");
        }

        using var buffer = new MemoryStream();
        await input.Content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        // 按内容检查：读不出字体家族名的不是字体（或已损坏），不保存。
        var families = FontRegistry.ReadFamilyNames(bytes);
        if (families.Count == 0)
        {
            throw new UserFriendlyException("不是有效的字体文件，或文件已损坏");
        }

        await files.SaveAsync(fileName, bytes);
        fonts.Reload();
        await operationLog.WriteAsync(LogType, fileName, $"上传，字体 {string.Join("、", families)}，{bytes.Length / 1024.0 / 1024.0:0.#} MB");
        return List();
    }

    /// <inheritdoc />
    public async Task<FontListDto> DeleteAsync(string fileName)
    {
        var item = fonts.FileDetails.FirstOrDefault(detail => string.Equals(Path.GetFileName(detail.Path), fileName, StringComparison.OrdinalIgnoreCase));
        if (item is null || !item.Uploaded)
        {
            throw AdminErrors.NotFoundError("字体不存在（部署目录里的字体不能删除）");
        }

        // 上传的字体整份读进了内存，文件没有被占用，可以先删文件再重新加载。
        files.Delete(Path.GetFileName(item.Path));
        fonts.Reload();
        await operationLog.WriteAsync(LogType, Path.GetFileName(item.Path), $"删除，字体 {string.Join("、", item.Families)}（引用它的模板生成时降级到默认字体）");
        return List();
    }

    /// <summary>
    /// 上传的文件名整理成能直接放进目录的名字：只留文件名部分，字母、数字、汉字、空格、点、连字符、下划线以外的字符换成下划线，最长 100 字。
    /// </summary>
    /// <param name="original">浏览器传来的文件名。</param>
    /// <param name="extension">小写的扩展名，如 .ttf。</param>
    /// <returns>安全的文件名，如 AlibabaPuHuiTi-Regular.ttf。</returns>
    private static string SafeFileName(string original, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(Path.GetFileName(original.Replace('\\', '/').Split('/')[^1]));
        var builder = new StringBuilder(stem.Length);
        foreach (var ch in stem)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is ' ' or '.' or '-' or '_' ? ch : '_');
        }

        var safe = builder.ToString().Trim(' ', '.');
        if (safe.Length == 0)
        {
            safe = "font";
        }

        return (safe.Length > 100 ? safe[..100] : safe) + extension;
    }
}
