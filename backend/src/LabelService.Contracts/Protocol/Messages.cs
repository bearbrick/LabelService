using System.Text.Json;
using LabelService.Contracts.Templates;

namespace LabelService.Contracts.Protocol;

/// <summary>
/// POST /api/v1/render 的请求体。
/// </summary>
public sealed class RenderApiRequest
{
    /// <summary>模板编码，必填。</summary>
    public string? TemplateCode
    {
        get; set;
    }

    /// <summary>模板版本号，不传则用最新发布版本。</summary>
    public int? Version
    {
        get; set;
    }

    /// <summary>pdf、png、zpl，必填。按字符串接收，非法值返回 FORMAT_INVALID。</summary>
    public string? Format
    {
        get; set;
    }

    /// <summary>203、300、600，不传则用模板设置。</summary>
    public int? Dpi
    {
        get; set;
    }

    /// <summary>仅 png 有效，true 时输出黑白图。</summary>
    public bool? Mono
    {
        get; set;
    }

    /// <summary>份数，默认 1，最大 100。</summary>
    public int? Copies
    {
        get; set;
    }

    /// <summary>字段数据。多传的 key 忽略。</summary>
    public Dictionary<string, JsonElement>? Data
    {
        get; set;
    }
}

/// <summary>
/// POST /api/v1/render/batch 的请求体。
/// </summary>
public sealed class BatchRenderApiRequest
{
    /// <summary>模板编码，必填。</summary>
    public string? TemplateCode
    {
        get; set;
    }

    /// <summary>模板版本号，不传则用最新发布版本。</summary>
    public int? Version
    {
        get; set;
    }

    /// <summary>pdf、png、zpl，必填。</summary>
    public string? Format
    {
        get; set;
    }

    /// <summary>203、300、600，不传则用模板设置。</summary>
    public int? Dpi
    {
        get; set;
    }

    /// <summary>仅 png 有效。</summary>
    public bool? Mono
    {
        get; set;
    }

    /// <summary>merge 或 zip，默认 merge；PNG 只能用 zip。</summary>
    public string? Output
    {
        get; set;
    }

    /// <summary>所有条目共用的字段，条目里的同名字段覆盖它。</summary>
    public Dictionary<string, JsonElement>? Common
    {
        get; set;
    }

    /// <summary>条目，输出顺序和这里一致。</summary>
    public List<BatchRenderApiItem>? Items
    {
        get; set;
    }
}

/// <summary>
/// 批量请求里的一条。
/// </summary>
public sealed class BatchRenderApiItem
{
    /// <summary>这一条的字段数据。</summary>
    public Dictionary<string, JsonElement>? Data
    {
        get; set;
    }

    /// <summary>份数，默认 1，最大 100。</summary>
    public int? Copies
    {
        get; set;
    }
}

/// <summary>
/// GET /api/v1/templates/{code}/schema 的响应体。
/// </summary>
public sealed class TemplateSchemaResponse
{
    /// <summary>模板编码。</summary>
    public string TemplateCode { get; set; } = "";

    /// <summary>模板显示名。</summary>
    public string Name { get; set; } = "";

    /// <summary>最新发布版本号。</summary>
    public int Version
    {
        get; set;
    }

    /// <summary>标签尺寸和默认 DPI。</summary>
    public LabelPage Page { get; set; } = new();

    /// <summary>字段定义（不含 sample，样例值统一放在 <see cref="Sample"/>）。</summary>
    public List<FieldDefinition> Fields { get; set; } = [];

    /// <summary>可以直接作为 data 传入的样例。</summary>
    public Dictionary<string, JsonElement> Sample { get; set; } = [];
}

/// <summary>
/// 所有失败响应的 JSON 结构。
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>错误码，见 <see cref="ErrorCodes"/>。</summary>
    public string Code { get; set; } = "";

    /// <summary>给人看的中文说明。</summary>
    public string Message { get; set; } = "";

    /// <summary>逐条、逐字段的明细；批量时带条目序号。</summary>
    public List<ErrorDetail>? Errors
    {
        get; set;
    }

    /// <summary>链路 ID，与响应头 X-Trace-Id 相同。</summary>
    public string? TraceId
    {
        get; set;
    }
}

/// <summary>
/// 错误明细。
/// </summary>
public sealed class ErrorDetail
{
    /// <summary>批量条目序号，从 0 开始；单张请求为 null。</summary>
    public int? Index
    {
        get; set;
    }

    /// <summary>字段 key 或请求参数名。</summary>
    public string? Field
    {
        get; set;
    }

    /// <summary>错误码。</summary>
    public string Code { get; set; } = "";

    /// <summary>给人看的中文说明。</summary>
    public string? Message
    {
        get; set;
    }
}
