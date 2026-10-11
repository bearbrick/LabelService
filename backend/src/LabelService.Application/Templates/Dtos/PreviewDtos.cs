using System.Text.Json;
using LabelService.Contracts.Templates;

namespace LabelService.Application.Templates.Dtos;

/// <summary>
/// 服务端预览的请求：设计器把还没保存的模板 JSON 和数据发过来，用服务端渲染引擎出 PNG。
/// </summary>
/// <param name="Template">模板 JSON（可以是还没保存的草稿）。</param>
/// <param name="Data">字段数据；不传时用字段的样例值。</param>
/// <param name="Dpi">203、300、600；不传时用模板的 DPI。</param>
/// <param name="Mono">true 时输出黑白图（热敏打印效果）。</param>
public sealed record PreviewInput(LabelTemplate? Template, Dictionary<string, JsonElement>? Data, int? Dpi, bool? Mono);
