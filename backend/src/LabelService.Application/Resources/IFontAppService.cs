using LabelService.Application.Resources.Dtos;

namespace LabelService.Application.Resources;

/// <summary>
/// 字体：服务端已加载的字体家族和文件（登录即可看），上传、删除字体（要权限点 res.manage，ADR 0011）。
/// </summary>
public interface IFontAppService
{
    /// <summary>
    /// 已加载的字体。
    /// </summary>
    /// <returns>字体家族名、文件和详情。</returns>
    FontListDto List();

    /// <summary>
    /// 上传字体（.ttf、.otf、.ttc、.otc，不超过 50 MB），立即生效，不用重启。
    /// </summary>
    /// <param name="input">上传的文件。</param>
    /// <returns>上传后的字体列表。</returns>
    Task<FontListDto> UploadAsync(UploadAssetInput? input);

    /// <summary>
    /// 删除上传的字体（部署目录里的不能删），立即生效；引用它的模板生成时降级到默认字体。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <returns>删除后的字体列表。</returns>
    Task<FontListDto> DeleteAsync(string fileName);
}
