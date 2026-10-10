namespace LabelService.Domain.Resources;

/// <summary>
/// 上传素材的元数据仓储（表 asset）。返回的实体会被跟踪，改完调用工作单元保存。
/// </summary>
public interface IAssetRepository
{
    /// <summary>
    /// 全部上传的素材，新的在前，不跟踪。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>素材列表。</returns>
    Task<IReadOnlyList<AssetEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按素材 Key 取素材。
    /// </summary>
    /// <param name="key">素材 Key。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>素材；不存在（或是内置素材）时为 null。</returns>
    Task<AssetEntity?> FindByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增素材。
    /// </summary>
    /// <param name="asset">新素材。</param>
    void Add(AssetEntity asset);

    /// <summary>
    /// 删除素材的元数据（文件由调用方删除）。
    /// </summary>
    /// <param name="asset">跟踪中的素材。</param>
    void Remove(AssetEntity asset);
}
