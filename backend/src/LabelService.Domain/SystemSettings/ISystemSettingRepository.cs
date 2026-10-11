namespace LabelService.Domain.SystemSettings;

/// <summary>
/// 系统设置的仓储（表 system_setting）。返回的实体会被跟踪，改完调用工作单元保存。
/// </summary>
public interface ISystemSettingRepository
{
    /// <summary>
    /// 全部已保存的设置项，跟踪。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>设置项列表；从没保存过时为空。</returns>
    Task<IReadOnlyList<SystemSettingEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增一个设置项。
    /// </summary>
    /// <param name="setting">新的设置项。</param>
    void Add(SystemSettingEntity setting);
}
