namespace LabelService.Domain.Layouts;

/// <summary>
/// 拼版版式的仓储（表 sheet_layout）。名字带 ForUpdate 的方法返回跟踪中的实体，改完调用工作单元保存。
/// </summary>
public interface ISheetLayoutRepository
{
    /// <summary>
    /// 全部版式（含停用的），按编码排序，不跟踪。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>版式列表。</returns>
    Task<IReadOnlyList<SheetLayoutEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 启用中的版式，不跟踪（启动时加载进内存快照）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>版式列表。</returns>
    Task<IReadOnlyList<SheetLayoutEntity>> ListEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按编码取版式，不跟踪。
    /// </summary>
    /// <param name="code">版式编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>版式；不存在时为 null。</returns>
    Task<SheetLayoutEntity?> FindAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按编码取版式，跟踪。
    /// </summary>
    /// <param name="code">版式编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>版式；不存在时为 null。</returns>
    Task<SheetLayoutEntity?> FindForUpdateAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 库里有没有版式（开发环境判断要不要导入示例版式）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>有版式时为 true。</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增版式。
    /// </summary>
    /// <param name="layout">新版式。</param>
    void Add(SheetLayoutEntity layout);

    /// <summary>
    /// 删除版式。
    /// </summary>
    /// <param name="layout">跟踪中的版式。</param>
    void Remove(SheetLayoutEntity layout);
}
