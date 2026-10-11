namespace LabelService.Domain.SystemSettings;

/// <summary>
/// 表 system_setting：一项系统设置（键值对），如系统名称、所属公司。没有保存过的项用默认值，见 <see cref="SystemInfoRules"/>。
/// </summary>
public sealed class SystemSettingEntity
{
    /// <summary>
    /// 设置项的键，如 system.name，见 <see cref="SystemInfoRules"/> 里的常量。
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 设置的值；空字符串表示不显示（系统名称除外，它不能为空）。
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// 最后修改时间（UTC）。
    /// </summary>
    public DateTime UpdatedAt
    {
        get; set;
    }
}
