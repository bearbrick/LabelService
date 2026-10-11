using System.Text.Json;
using LabelService.Contracts.Layouts;

namespace LabelService.Application.Layouts.Dtos;

/// <summary>
/// 版式（列表和详情的一项）：版式 JSON 加上启用状态和修改记录。
/// </summary>
/// <param name="Layout">版式 JSON（规范见 docs/contracts.md“拼版”）。</param>
/// <param name="Enabled">是否启用；停用的版式开放接口不能用。</param>
/// <param name="UpdatedBy">最后修改人账号。</param>
/// <param name="UpdatedAt">最后修改时间（UTC）。</param>
public sealed record SheetLayoutDto(SheetLayout Layout, bool Enabled, string UpdatedBy, DateTime UpdatedAt);

/// <summary>
/// 新建或修改版式。
/// </summary>
/// <param name="Layout">版式 JSON；修改时编码不能变。</param>
/// <param name="Enabled">是否启用，默认启用。</param>
public sealed record SaveSheetLayoutDto(SheetLayout? Layout, bool? Enabled);

/// <summary>
/// 版式预览：还没保存的版式 + 已发布的模板 + 数据。
/// </summary>
/// <param name="Layout">版式 JSON（可以是还没保存的）。</param>
/// <param name="TemplateCode">模板编码，必须已发布且在自己的业务范围里。</param>
/// <param name="Version">模板版本；不传用最新发布版本。</param>
/// <param name="Format">png（多页时 ZIP）或 pdf，默认 png。</param>
/// <param name="Dpi">203、300、600，默认 300。</param>
/// <param name="StartSlot">从第几个格子开始放（用过一部分的标签纸），默认 1。</param>
/// <param name="Items">每条数据和份数；不传时用样例值把一页剩下的格子铺满。</param>
public sealed record SheetLayoutPreviewInput(
    SheetLayout? Layout,
    string? TemplateCode,
    int? Version,
    string? Format,
    int? Dpi,
    int? StartSlot,
    List<SheetLayoutPreviewItem>? Items);

/// <summary>
/// 版式预览的一条数据。
/// </summary>
/// <param name="Data">字段数据。</param>
/// <param name="Copies">份数，默认 1。</param>
public sealed record SheetLayoutPreviewItem(Dictionary<string, JsonElement>? Data, int? Copies);

/// <summary>
/// 打印校准测试页。
/// </summary>
/// <param name="Layout">版式 JSON（可以是还没保存的）。</param>
/// <param name="Format">pdf（默认）或 png。</param>
/// <param name="Dpi">203、300、600，默认 300。</param>
public sealed record SheetLayoutTestPageInput(SheetLayout? Layout, string? Format, int? Dpi);
