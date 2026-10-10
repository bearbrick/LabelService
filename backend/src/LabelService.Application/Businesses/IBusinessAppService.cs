using LabelService.Application.Businesses.Dtos;

namespace LabelService.Application.Businesses;

/// <summary>
/// 业务分组：列表按当前用户的业务范围过滤（登录即可看）；新建、修改、删除要权限点 biz.manage。
/// </summary>
public interface IBusinessAppService
{
    /// <summary>
    /// 当前用户业务范围里的业务分组，按排序号、ID 排序，带模板数。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>业务分组列表。</returns>
    Task<IReadOnlyList<BusinessDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新建业务分组，排在最后。
    /// </summary>
    /// <param name="input">编码、名称、说明、负责人、标识色。</param>
    /// <returns>新分组。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">编码或名称不合法、编码重复、负责人不存在。</exception>
    Task<BusinessDto> CreateAsync(SaveBusinessDto input);

    /// <summary>
    /// 修改业务分组。
    /// </summary>
    /// <param name="id">业务分组 ID。</param>
    /// <param name="input">新的编码、名称、说明、负责人、标识色。</param>
    /// <returns>修改后的分组。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在业务范围里（NOT_FOUND）、内容不合法。</exception>
    Task<BusinessDto> UpdateAsync(int id, SaveBusinessDto input);

    /// <summary>
    /// 删除业务分组。分组里还有模板时不能删；已删除的模板解除关联，用户和调用方的业务范围里去掉这个分组。
    /// </summary>
    /// <param name="id">业务分组 ID。</param>
    /// <returns>删除完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在业务范围里（NOT_FOUND）、分组里还有模板。</exception>
    Task DeleteAsync(int id);
}
