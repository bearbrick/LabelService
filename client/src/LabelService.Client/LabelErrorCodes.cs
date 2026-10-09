namespace LabelService.Client;

/// <summary>
/// 错误码常量，和服务端开放接口一一对应，另有几个只在客户端产生的错误码。
/// </summary>
public static class LabelErrorCodes
{
    /// <summary>401：API Key 缺失、填错、被停用或已重置。</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>403：这个 Key 无权使用该模板。</summary>
    public const string TemplateForbidden = "TEMPLATE_FORBIDDEN";

    /// <summary>404：模板或版本不存在，或者还没有发布过。</summary>
    public const string TemplateNotFound = "TEMPLATE_NOT_FOUND";

    /// <summary>400：请求体不是合法 JSON。</summary>
    public const string InvalidJson = "INVALID_JSON";

    /// <summary>400：format 取值不对。</summary>
    public const string FormatInvalid = "FORMAT_INVALID";

    /// <summary>400：dpi 取值不对。</summary>
    public const string DpiInvalid = "DPI_INVALID";

    /// <summary>400：output 取值不对，或 PNG 批量没用 Zip。</summary>
    public const string OutputInvalid = "OUTPUT_INVALID";

    /// <summary>400：份数不在 1–100。</summary>
    public const string CopiesInvalid = "COPIES_INVALID";

    /// <summary>400：缺少必填字段，明细见 <see cref="LabelServiceException.Errors"/>。</summary>
    public const string FieldRequired = "FIELD_REQUIRED";

    /// <summary>400：字段值的类型、格式或长度不对。</summary>
    public const string FieldTypeInvalid = "FIELD_TYPE_INVALID";

    /// <summary>400：条码内容不符合码制。</summary>
    public const string BarcodeInvalid = "BARCODE_INVALID";

    /// <summary>400：内容太长，框内放不下能扫的条码。</summary>
    public const string BarcodeTooDense = "BARCODE_TOO_DENSE";

    /// <summary>400：图片解码失败或超过 1 MB。</summary>
    public const string ImageInvalid = "IMAGE_INVALID";

    /// <summary>413：单次总张数超过上限，请拆批。</summary>
    public const string BatchTooLarge = "BATCH_TOO_LARGE";

    /// <summary>500：服务端渲染内部错误，带 traceId 联系标签服务管理员。</summary>
    public const string RenderFailed = "RENDER_FAILED";

    /// <summary>客户端：连不上服务、DNS 失败等网络错误，已自动重试。</summary>
    public const string NetworkError = "NETWORK_ERROR";

    /// <summary>客户端：超过 <see cref="LabelServiceClientOptions.Timeout"/>，已自动重试。</summary>
    public const string Timeout = "TIMEOUT";

    /// <summary>客户端：服务返回 502、503、504，已自动重试。</summary>
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    /// <summary>客户端：服务返回了无法识别的响应（如代理返回的 HTML 错误页）。</summary>
    public const string UnexpectedResponse = "UNEXPECTED_RESPONSE";
}
