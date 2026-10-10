using LabelService.Application.Layouts.Dtos;
using LabelService.Application.Rendering.Dtos;

namespace LabelService.Application.Layouts;

/// <summary>
/// 拼版版式（ADR 0005）：查看登录即可，增删改要权限点 res.manage；预览和打印校准测试页给拼版设计器用。
/// </summary>
/// <remarks>
/// 版式 JSON 规范见 docs/contracts.md“拼版”。保存后立即更新内存缓存，开放接口马上能用。
/// </remarks>
public interface ISheetLayoutAppService
{
    /// <summary>
    /// 版式列表（含停用的），按编码排序；给了格子尺寸时只列格子尺寸相同的（模板详情页“可用的版式”）。
    /// </summary>
    /// <param name="slotWidth">格子宽度，mm。</param>
    /// <param name="slotHeight">格子高度，mm。</param>
    /// <returns>版式列表。</returns>
    Task<IReadOnlyList<SheetLayoutDto>> ListAsync(double? slotWidth, double? slotHeight);

    /// <summary>
    /// 按编码取版式。
    /// </summary>
    /// <param name="code">版式编码。</param>
    /// <returns>版式。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在（NOT_FOUND）。</exception>
    Task<SheetLayoutDto> GetAsync(string code);

    /// <summary>
    /// 新建版式。
    /// </summary>
    /// <param name="input">版式 JSON 和启用状态。</param>
    /// <returns>保存后的版式。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">缺少版式、检查不通过、编码已存在。</exception>
    Task<SheetLayoutDto> CreateAsync(SaveSheetLayoutDto input);

    /// <summary>
    /// 修改版式。
    /// </summary>
    /// <param name="code">版式编码，和请求体里的必须一致。</param>
    /// <param name="input">版式 JSON 和启用状态。</param>
    /// <returns>保存后的版式。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">缺少版式、改了编码、检查不通过、不存在（NOT_FOUND）。</exception>
    Task<SheetLayoutDto> UpdateAsync(string code, SaveSheetLayoutDto input);

    /// <summary>
    /// 删除版式。
    /// </summary>
    /// <param name="code">版式编码。</param>
    /// <returns>完成时完成。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在（NOT_FOUND）。</exception>
    Task DeleteAsync(string code);

    /// <summary>
    /// 预览：还没保存的版式 + 已发布的模板 + 数据，返回 PNG（多页时 ZIP）或 PDF。
    /// </summary>
    /// <param name="input">版式、模板、格式、DPI、起始格子和数据。</param>
    /// <returns>文件和响应头要用的张数、页数、DPI、各阶段耗时、提示。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">版式不合法、格式或 DPI 不支持、模板不存在（NOT_FOUND）或不在范围里（FORBIDDEN）、尺寸不符、数据不合法、渲染失败。</exception>
    RenderedLabelDto Preview(SheetLayoutPreviewInput input);

    /// <summary>
    /// 打印校准测试页：每个格子画边框、中心十字和序号，默认 PDF。
    /// </summary>
    /// <param name="input">版式、格式、DPI。</param>
    /// <returns>文件和响应头要用的张数、页数、DPI、各阶段耗时、提示。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">版式不合法、格式或 DPI 不支持、渲染失败。</exception>
    RenderedLabelDto TestPage(SheetLayoutTestPageInput input);
}
