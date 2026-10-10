using LabelService.Application.Resources.Dtos;

namespace LabelService.Application.Resources;

/// <summary>
/// 画布预设：查看登录即可（设计器要用），增删改要权限点 res.manage。
/// </summary>
public interface ICanvasPresetAppService
{
    /// <summary>
    /// 全部预设，按排序号、ID 排序。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>预设列表。</returns>
    Task<IReadOnlyList<CanvasPresetDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新建预设，排在最后。
    /// </summary>
    /// <param name="input">名称、类型、宽高、是否启用。</param>
    /// <returns>新预设。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">名称为空或太长、宽高超出范围。</exception>
    Task<CanvasPresetDto> CreateAsync(SaveCanvasPresetDto input);

    /// <summary>
    /// 修改预设。
    /// </summary>
    /// <param name="id">预设 ID。</param>
    /// <param name="input">名称、类型、宽高、是否启用。</param>
    /// <returns>修改后的预设。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在（NOT_FOUND）、内容不合法。</exception>
    Task<CanvasPresetDto> UpdateAsync(int id, SaveCanvasPresetDto input);

    /// <summary>
    /// 删除预设。选中预设后宽高已经写进模板，删预设不影响已有模板。
    /// </summary>
    /// <param name="id">预设 ID。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在（NOT_FOUND）。</exception>
    Task DeleteAsync(int id);
}
