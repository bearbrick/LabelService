namespace LabelService.Domain.Resources;

/// <summary>
/// 画布预设的仓储（表 canvas_preset）。返回的实体会被跟踪，改完调用工作单元保存。
/// </summary>
public interface ICanvasPresetRepository
{
    /// <summary>
    /// 全部预设，按排序号、ID 排序，不跟踪。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>预设列表。</returns>
    Task<IReadOnlyList<CanvasPresetEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 ID 取预设。
    /// </summary>
    /// <param name="id">预设 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>预设；不存在时为 null。</returns>
    Task<CanvasPresetEntity?> FindAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 最大的排序号（新建的排在最后）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>最大排序号；没有预设时为 0。</returns>
    Task<int> MaxSortAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增预设。
    /// </summary>
    /// <param name="preset">新预设。</param>
    void Add(CanvasPresetEntity preset);

    /// <summary>
    /// 删除预设。
    /// </summary>
    /// <param name="preset">跟踪中的预设。</param>
    void Remove(CanvasPresetEntity preset);
}
