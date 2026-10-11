using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;
using LabelService.Application.Resources.Dtos;
using LabelService.Contracts.Protocol;
using LabelService.Domain.Shared;
using LabelService.Rendering.Assets;
using SkiaSharp;

namespace LabelService.Application.Resources;

/// <summary>
/// 上传图片的共用检查：素材和公司 Logo 都只收 PNG、JPG、SVG，不超过 1 MB，按文件内容判断类型，SVG 清洗。
/// </summary>
internal static class UploadedImages
{
    /// <summary>
    /// 读出上传的图片并检查。
    /// </summary>
    /// <param name="input">上传的文件；null 或空文件时报错。</param>
    /// <returns>要保存的内容（SVG 是清洗过的）和扩展名（.png、.jpg、.svg）。</returns>
    /// <exception cref="UserFriendlyException">没选文件、超过 1 MB、不是支持的格式或无法解码。</exception>
    public static async Task<(byte[] Bytes, string Extension)> ReadAsync([NotNull] UploadAssetInput? input)
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
        return Identify(buffer.ToArray());
    }

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
