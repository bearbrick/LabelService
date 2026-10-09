namespace LabelService.Contracts.Protocol;

/// <summary>
/// 开放接口的错误码。新增、改名或改 HTTP 状态码属于契约变更：同时改客户端 SDK 的 LabelErrorCodes、docs/contracts.md 和设计书第 8 节。
/// </summary>
/// <remarks>
/// 检查程序会核对这里的每个错误码都在 SDK 里有同名常量。
/// </remarks>
public static class ErrorCodes
{
    /// <summary>401：API Key 缺失或无效。</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>403：这个 Key 无权使用该模板。</summary>
    public const string TemplateForbidden = "TEMPLATE_FORBIDDEN";

    /// <summary>404：模板或版本不存在，或者还没有发布过。</summary>
    public const string TemplateNotFound = "TEMPLATE_NOT_FOUND";

    /// <summary>400：请求体不是合法 JSON，或缺少 templateCode、data 等必要结构。</summary>
    public const string InvalidJson = "INVALID_JSON";

    /// <summary>400：format 不是 pdf、png、zpl 之一。</summary>
    public const string FormatInvalid = "FORMAT_INVALID";

    /// <summary>400：dpi 不是 203、300、600 之一。</summary>
    public const string DpiInvalid = "DPI_INVALID";

    /// <summary>400：output 不是 merge、zip 之一，或 PNG 批量没用 zip。</summary>
    public const string OutputInvalid = "OUTPUT_INVALID";

    /// <summary>400：copies 不在 1–100。</summary>
    public const string CopiesInvalid = "COPIES_INVALID";

    /// <summary>400：缺少必填字段。</summary>
    public const string FieldRequired = "FIELD_REQUIRED";

    /// <summary>400：字段值的类型、格式或长度不对。</summary>
    public const string FieldTypeInvalid = "FIELD_TYPE_INVALID";

    /// <summary>400：条码内容不符合码制。</summary>
    public const string BarcodeInvalid = "BARCODE_INVALID";

    /// <summary>400：内容太长，框内放不下能扫的条码。</summary>
    public const string BarcodeTooDense = "BARCODE_TOO_DENSE";

    /// <summary>400：图片解码失败或超过 1 MB。</summary>
    public const string ImageInvalid = "IMAGE_INVALID";

    /// <summary>413：单次总张数（含份数）超过上限。</summary>
    public const string BatchTooLarge = "BATCH_TOO_LARGE";

    /// <summary>500：渲染内部错误，凭 traceId 查日志。</summary>
    public const string RenderFailed = "RENDER_FAILED";

    /// <summary>
    /// 全部错误码及其 HTTP 状态码。
    /// </summary>
    public static IReadOnlyDictionary<string, int> StatusCodes
    {
        get;
    } = new Dictionary<string, int>
    {
        [Unauthorized] = 401,
        [TemplateForbidden] = 403,
        [TemplateNotFound] = 404,
        [InvalidJson] = 400,
        [FormatInvalid] = 400,
        [DpiInvalid] = 400,
        [OutputInvalid] = 400,
        [CopiesInvalid] = 400,
        [FieldRequired] = 400,
        [FieldTypeInvalid] = 400,
        [BarcodeInvalid] = 400,
        [BarcodeTooDense] = 400,
        [ImageInvalid] = 400,
        [BatchTooLarge] = 413,
        [RenderFailed] = 500,
    };
}
