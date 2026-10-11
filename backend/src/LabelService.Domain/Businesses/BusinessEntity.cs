namespace LabelService.Domain.Businesses;

/// <summary>
/// 表 business：业务分组。模板按业务分组归类，用户和调用方按业务分组授权（ADR 0006）。
/// </summary>
public sealed class BusinessEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// 编码，全局唯一，大写字母开头，如 PACK。
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// 名称，如“成品包装”。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 说明。
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 负责人（用户 ID），可空。
    /// </summary>
    public int? OwnerUserId
    {
        get; set;
    }

    /// <summary>
    /// 标识色，#RRGGBB。
    /// </summary>
    public string Color { get; set; } = "#00939A";

    /// <summary>
    /// 排序，小的在前。
    /// </summary>
    public int Sort
    {
        get; set;
    }

    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}
