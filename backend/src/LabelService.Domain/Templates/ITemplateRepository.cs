namespace LabelService.Domain.Templates;

/// <summary>
/// 模板和模板版本的仓储（表 label_template、label_template_version）。
/// </summary>
/// <remarks>
/// 名字带 ForUpdate 的方法返回跟踪中的实体，改完调用工作单元保存；其余方法只读、不跟踪。
/// 已删除的模板（软删除）除非特别说明，一律当作不存在。
/// </remarks>
public interface ITemplateRepository
{
    /// <summary>
    /// 按编码取模板（不含已删除的），不跟踪。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模板；不存在时为 null。</returns>
    Task<TemplateEntity?> FindAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按编码取模板（包括已删除的，保存草稿时要据此报“编码属于已删除的模板”），连同它的草稿，跟踪。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模板；不存在时为 null。<see cref="TemplateEntity.Versions"/> 里只有草稿（最多一个）。</returns>
    Task<TemplateEntity?> FindWithDraftForUpdateAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按编码取模板（不含已删除的），跟踪。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模板；不存在时为 null。</returns>
    Task<TemplateEntity?> FindForUpdateAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 模板的草稿版本，跟踪（放弃草稿时删除它）。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>草稿；没有时为 null。</returns>
    Task<TemplateVersionEntity?> FindDraftForUpdateAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 模板列表（不含已删除的），按编码排序。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>每个模板的概要。</returns>
    Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 模板库用的列表：概要加上当前内容（有草稿取草稿，否则取最新发布版本）的 JSON。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>每个模板一项；从未保存过内容的模板 JSON 为 null。</returns>
    Task<IReadOnlyList<(TemplateSummary Summary, string? ContentJson)>> ListWithContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 草稿的模板 JSON。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>JSON；没有草稿时为 null。</returns>
    Task<string?> GetDraftContentAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 某个已发布版本的模板 JSON。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="versionNo">版本号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>JSON；版本不存在或还是草稿时为 null。</returns>
    Task<string?> GetPublishedContentAsync(string code, int versionNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全部版本（含草稿），新的在前。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>版本概要。</returns>
    Task<IReadOnlyList<TemplateVersionSummary>> ListVersionsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全部已发布版本（不含已删除的模板），启动时加载进内存缓存。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>每个已发布版本一项。</returns>
    Task<IReadOnlyList<PublishedTemplateRow>> ListPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 库里有没有模板（包括已删除的；开发环境首次建库时判断要不要导入示例）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>有模板时为 true。</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 每个业务分组里有几个模板（不含已删除的）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>业务分组 ID → 模板数。</returns>
    Task<IReadOnlyDictionary<int, int>> CountByBusinessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 业务分组里还有没有模板（不含已删除的）。
    /// </summary>
    /// <param name="businessId">业务分组 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>有时为 true。</returns>
    Task<bool> AnyInBusinessAsync(int businessId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解除模板和业务分组的关联（删除分组前，对已删除的模板做；立即执行，不经过工作单元）。
    /// </summary>
    /// <param name="businessId">业务分组 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行完成时完成。</returns>
    Task DetachBusinessAsync(int businessId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增模板（连同它的版本）。
    /// </summary>
    /// <param name="template">新模板。</param>
    void Add(TemplateEntity template);

    /// <summary>
    /// 删除一个版本（只能是草稿；已发布的版本数据库上下文会拦）。
    /// </summary>
    /// <param name="version">跟踪中的版本。</param>
    void RemoveVersion(TemplateVersionEntity version);
}
