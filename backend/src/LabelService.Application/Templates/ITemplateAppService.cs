using LabelService.Application.Templates.Dtos;
using LabelService.Contracts.Templates;

namespace LabelService.Application.Templates;

/// <summary>
/// 模板管理：列表、打开、缩略图、新建、保存草稿、放弃草稿、发布、版本、回滚、换分组、删除、发布前检查。
/// </summary>
/// <remarks>
/// 权限点由接口层检查；这里检查业务范围：不在范围里的模板当作不存在（NOT_FOUND），不暴露是否存在。
/// 保存草稿很频繁（设计器自动保存），操作日志同一个人同一个模板 10 分钟只记一条。
/// </remarks>
public interface ITemplateAppService
{
    /// <summary>
    /// 当前用户业务范围里的模板，按编码排序，带画布和字段、元素个数。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模板列表。</returns>
    Task<IReadOnlyList<TemplateListItemDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 打开模板：有草稿时给草稿，没有时给最新发布版本的内容。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>模板信息和内容。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）。</exception>
    Task<TemplateDetailDto> GetAsync(string code);

    /// <summary>
    /// 缩略图（PNG，按模板编码和修改时间缓存）。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>PNG；没有内容或渲染失败时为 null（前端显示占位图）。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）。</exception>
    Task<byte[]?> GetThumbnailAsync(string code);

    /// <summary>
    /// 新建模板（保存成草稿）。
    /// </summary>
    /// <param name="input">编码、名称、业务分组和内容或尺寸。</param>
    /// <returns>模板编码。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">业务分组不在范围里（FORBIDDEN）、编码已存在或不合法。</exception>
    Task<TemplateCreatedDto> CreateAsync(CreateTemplateDto input);

    /// <summary>
    /// 保存草稿（设计器自动保存）。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="content">模板内容，编码必须和 <paramref name="code"/> 一致。</param>
    /// <returns>草稿的版本号和保存时间。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）、改了编码、并发冲突。</exception>
    Task<DraftSavedDto> SaveDraftAsync(string code, LabelTemplate content);

    /// <summary>
    /// 放弃未发布的草稿。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）、没有草稿、并发冲突。</exception>
    Task DiscardDraftAsync(string code);

    /// <summary>
    /// 发布草稿。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="input">发布说明，可空。</param>
    /// <returns>新版本号、提示和试渲染耗时。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）、没有草稿、检查不通过、并发冲突。</exception>
    Task<PublishResultDto> PublishAsync(string code, PublishTemplateDto? input);

    /// <summary>
    /// 版本记录（含草稿），新的在前。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>版本列表。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）。</exception>
    Task<IReadOnlyList<TemplateVersionDto>> ListVersionsAsync(string code);

    /// <summary>
    /// 某个已发布版本的内容。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="versionNo">版本号。</param>
    /// <returns>模板内容。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">模板或版本不存在（NOT_FOUND）。</exception>
    Task<LabelTemplate> GetVersionAsync(string code, int versionNo);

    /// <summary>
    /// 把历史版本恢复成草稿（回滚的第一步，确认后再发布）。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="versionNo">要恢复的版本号。</param>
    /// <returns>草稿的版本号。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）、没有这个版本、并发冲突。</exception>
    Task<DraftRestoredDto> RestoreAsync(string code, int versionNo);

    /// <summary>
    /// 换业务分组。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="input">新的业务分组。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）、目标分组不在范围里（FORBIDDEN）、并发冲突。</exception>
    Task MoveAsync(string code, MoveTemplateDto input);

    /// <summary>
    /// 删除模板（软删除），开放接口立即不能再调用。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或不在范围里（NOT_FOUND）、并发冲突。</exception>
    Task DeleteAsync(string code);

    /// <summary>
    /// 发布前检查（不保存）：静态规则 + 试渲染计时。设计器的“检查”按钮和发布对话框用。
    /// </summary>
    /// <param name="content">模板内容。</param>
    /// <returns>错误、提示和试渲染耗时。</returns>
    TemplateCheckResultDto Check(LabelTemplate content);
}
