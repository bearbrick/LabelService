using System.Collections.Frozen;
using System.Globalization;

namespace LabelService.Domain.Shared;

/// <summary>
/// 逗号分隔的 ID 列表（如用户、调用方能访问的业务分组 ID “3,5,8”）的解析和格式化。
/// </summary>
public static class IdList
{
    /// <summary>
    /// 解析逗号分隔的 ID 列表，忽略空项和非法项。
    /// </summary>
    /// <param name="value">如 “3,5,8”。</param>
    /// <returns>ID 集合，只读。</returns>
    public static IReadOnlySet<int> Parse(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : (int?)null)
            .OfType<int>()
            .ToFrozenSet();

    /// <summary>
    /// 把 ID 写成逗号分隔的字符串，按原顺序，不去重。
    /// </summary>
    /// <param name="ids">ID。</param>
    /// <returns>如 “3,5,8”；没有 ID 时为空字符串。</returns>
    public static string Format(IEnumerable<int> ids) => string.Join(",", ids);

    /// <summary>
    /// 从列表里去掉一个 ID，剩下的按从小到大排列。
    /// </summary>
    /// <param name="value">逗号分隔的 ID 列表。</param>
    /// <param name="id">要去掉的 ID。</param>
    /// <returns>去掉后的列表。</returns>
    public static string Without(string value, int id) => Format(Parse(value).Where(item => item != id).Order());
}
