using LabelService.Application.Resources.Dtos;

namespace LabelService.Application.Resources;

/// <summary>
/// 图片素材：查看登录即可（设计器要用），上传、改名、删除要权限点 res.manage。
/// </summary>
/// <remarks>
/// 上传：PNG、JPG、SVG，不超过 1 MB；按文件内容判断类型（不信扩展名），SVG 先清洗再保存。
/// 保存后重新加载素材库，模板里写 asset:Key 立即可用。
/// </remarks>
public interface IAssetAppService
{
    /// <summary>
    /// 素材列表：先列内置素材（按 Key 排序），再列上传的（新的在前）。上传的素材和内置素材同 Key 时只列上传的。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>素材列表。</returns>
    Task<IReadOnlyList<AssetDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 素材文件，设计器显示用。
    /// </summary>
    /// <param name="key">素材 Key。</param>
    /// <returns>文件内容、类型和修改时间。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">Key 不合法或素材不存在（NOT_FOUND）。</exception>
    AssetFileDto GetFile(string key);

    /// <summary>
    /// 上传素材。
    /// </summary>
    /// <param name="input">上传的文件；没选文件时为 null。</param>
    /// <returns>新素材。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">没选文件、超过 1 MB、不是 PNG/JPG/SVG、图片无法解码。</exception>
    Task<AssetDto> UploadAsync(UploadAssetInput? input);

    /// <summary>
    /// 上传的素材改名（内置素材不能改名）。
    /// </summary>
    /// <param name="key">素材 Key。</param>
    /// <param name="input">新名称。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">名称为空或太长、素材不存在（NOT_FOUND）。</exception>
    Task RenameAsync(string key, RenameAssetDto input);

    /// <summary>
    /// 删除上传的素材（内置素材不能删除）。引用它的模板生成时会报 IMAGE_INVALID。
    /// </summary>
    /// <param name="key">素材 Key。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">素材不存在（NOT_FOUND）。</exception>
    Task DeleteAsync(string key);
}
