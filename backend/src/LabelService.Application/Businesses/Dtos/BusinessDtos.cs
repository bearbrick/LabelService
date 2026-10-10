namespace LabelService.Application.Businesses.Dtos;

/// <summary>
/// 业务分组（模板库左侧的分组、业务分组管理页的一行）。
/// </summary>
/// <param name="Id">业务分组 ID。</param>
/// <param name="Code">编码，大写。</param>
/// <param name="Name">名称。</param>
/// <param name="Description">说明。</param>
/// <param name="OwnerUserId">负责人的用户 ID。</param>
/// <param name="Color">标识色 #RRGGBB。</param>
/// <param name="Templates">分组里的模板数（不含已删除的）。</param>
public sealed record BusinessDto(int Id, string Code, string Name, string Description, int? OwnerUserId, string Color, int Templates);

/// <summary>
/// 新建或修改业务分组。
/// </summary>
/// <param name="Code">编码：大写字母开头，只含大写字母、数字、下划线，最长 32 位；输入小写会转成大写。</param>
/// <param name="Name">名称，不能为空，最长 64 字。</param>
/// <param name="Description">说明。</param>
/// <param name="OwnerUserId">负责人的用户 ID，可空。</param>
/// <param name="Color">标识色 #RRGGBB；格式不对时保持原值。</param>
public sealed record SaveBusinessDto(string? Code, string? Name, string? Description, int? OwnerUserId, string? Color);
