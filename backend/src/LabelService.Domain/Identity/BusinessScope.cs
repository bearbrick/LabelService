using LabelService.Domain.Shared;

namespace LabelService.Domain.Identity;

/// <summary>
/// 业务范围：一个用户（或调用方）能访问哪些业务分组。全部，或者若干个分组（ADR 0006）。
/// </summary>
/// <param name="AllBusinesses">能访问全部业务分组（包括以后新建的）。</param>
/// <param name="Businesses">能访问的业务分组 ID；<paramref name="AllBusinesses"/> 为 true 时不看。</param>
public sealed record BusinessScope(bool AllBusinesses, IReadOnlySet<int> Businesses)
{
    /// <summary>
    /// 从实体里存的两列（是否全部、逗号分隔的 ID）创建。
    /// </summary>
    /// <param name="allBusinesses">是否全部业务。</param>
    /// <param name="businessIds">逗号分隔的业务分组 ID。</param>
    /// <returns>业务范围。</returns>
    public static BusinessScope From(bool allBusinesses, string businessIds) => new(allBusinesses, IdList.Parse(businessIds));

    /// <summary>
    /// 业务分组在不在范围里。没有分组的东西（如没分组的模板）只有全部范围能访问。
    /// </summary>
    /// <param name="businessId">业务分组 ID，可空。</param>
    /// <returns>在范围里时为 true。</returns>
    public bool Contains(int? businessId) => AllBusinesses || (businessId is { } id && Businesses.Contains(id));

    /// <summary>
    /// 另一个业务范围是否完全在这个范围里：全部范围包含一切；部分范围不包含“全部”，只包含自己有的分组。
    /// </summary>
    /// <param name="allBusinesses">另一个范围是否是全部业务。</param>
    /// <param name="businessIds">另一个范围的业务分组 ID。</param>
    /// <returns>完全包含时为 true。</returns>
    public bool Covers(bool allBusinesses, IEnumerable<int> businessIds) =>
        AllBusinesses || (!allBusinesses && businessIds.All(id => Businesses.Contains(id)));
}
