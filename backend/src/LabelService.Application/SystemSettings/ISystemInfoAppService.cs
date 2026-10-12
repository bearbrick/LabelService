using LabelService.Application.Resources.Dtos;
using LabelService.Application.SystemSettings.Dtos;

namespace LabelService.Application.SystemSettings;

/// <summary>
/// 系统信息：系统名称、副标题、所属公司、版权信息、技术支持、公司 Logo 和 Logo 图标。读取不用登录（登录页要显示），修改要权限点 sys.settings。
/// </summary>
public interface ISystemInfoAppService
{
    /// <summary>
    /// 当前的系统信息；没保存过的项用默认值。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>系统信息。</returns>
    Task<SystemInfoDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 修改文字部分；有变化的项记一条操作日志。
    /// </summary>
    /// <param name="input">新的系统信息。</param>
    /// <returns>修改后的系统信息。</returns>
    Task<SystemInfoDto> SaveAsync(SaveSystemInfoDto? input);

    /// <summary>
    /// 当前的公司 Logo 文件。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>图片文件。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">还没上传 Logo（NOT_FOUND）。</exception>
    Task<AssetFileDto> GetLogoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 上传公司 Logo（PNG、JPG、SVG、WebP，不超过 1 MB），替换原来的。
    /// </summary>
    /// <param name="input">上传的文件。</param>
    /// <returns>修改后的系统信息（Logo 版本变了）。</returns>
    Task<SystemInfoDto> UploadLogoAsync(UploadAssetInput? input);

    /// <summary>
    /// 删除上传的 Logo，恢复成占位。
    /// </summary>
    /// <returns>修改后的系统信息。</returns>
    Task<SystemInfoDto> ResetLogoAsync();

    /// <summary>
    /// 当前的 Logo 图标文件（顶栏、浏览器标签页用）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>图片文件。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">还没上传图标（NOT_FOUND）。</exception>
    Task<AssetFileDto> GetIconAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 上传 Logo 图标（PNG、JPG、SVG、WebP，不超过 1 MB），替换原来的。
    /// </summary>
    /// <param name="input">上传的文件。</param>
    /// <returns>修改后的系统信息（图标版本变了）。</returns>
    Task<SystemInfoDto> UploadIconAsync(UploadAssetInput? input);

    /// <summary>
    /// 删除上传的 Logo 图标，顶栏和浏览器标签页改用完整 Logo。
    /// </summary>
    /// <returns>修改后的系统信息。</returns>
    Task<SystemInfoDto> ResetIconAsync();
}
