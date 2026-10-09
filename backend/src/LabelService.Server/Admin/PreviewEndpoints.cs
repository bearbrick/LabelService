using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using LabelService.Server.Diagnostics;
using LabelService.Server.Templates;

namespace LabelService.Server.Admin;

/// <summary>
/// 服务端预览（tpl.view）：设计器把未保存的模板 JSON 和样例数据发过来，用服务端渲染引擎出 PNG，保证所见即所得（T-016）。
/// </summary>
/// <remarks>
/// 模板不经过存储，直接检查后渲染。放在管理接口而不是开放接口，因为它要登录，而登录态和 API Key 互不相通（ADR 0006、ADR 0007）。
/// 指标只记 endpoint=preview 的请求数和总耗时，不影响开放接口的指标；不写调用日志。
/// </remarks>
internal static class PreviewEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api) =>
        api.MapPost("/preview", PreviewAsync).RequireAdmin(Permissions.TemplateView);

    private static async Task<IResult> PreviewAsync(HttpContext http, ILabelRenderer renderer, LabelMetrics metrics)
    {
        var started = Stopwatch.GetTimestamp();
        PreviewRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PreviewRequest>(http.Request.Body, LabelJson.Options, http.RequestAborted);
        }
        catch (JsonException ex)
        {
            return AdminResults.Invalid(JsonErrorMessage(ex), [ex.Message]);
        }

        var template = body?.Template;
        if (template is null)
        {
            return AdminResults.Invalid("请求体缺少 template（模板 JSON）");
        }

        var errors = TemplatePublishValidator.PreviewErrors(template);
        if (errors.Count > 0)
        {
            return AdminResults.Invalid("模板有问题，不能预览：" + errors[0], errors);
        }

        var dpi = body!.Dpi ?? (template.Page.Dpi > 0 ? template.Page.Dpi : RenderLimits.DefaultDpi);
        if (!RenderLimits.AllowedDpi.Contains(dpi))
        {
            return AdminResults.Invalid($"dpi 只能是 {string.Join("、", RenderLimits.AllowedDpi)}");
        }

        var data = body.Data ?? TemplatePublishValidator.SampleData(template.Fields, []);
        var validation = FieldValidator.Validate(template.Fields, null, data);
        if (!validation.IsValid)
        {
            return AdminResults.Invalid("数据不合法：" + (validation.Errors[0].Message ?? validation.Errors[0].Code), validation.Errors.Select(error => $"{error.Code}：{error.Message}").ToArray());
        }

        RenderResult result;
        try
        {
            result = renderer.Render(new RenderJob
            {
                Template = template,
                Labels = [new LabelInstance(validation.Values)],
                Format = LabelFormat.Png,
                Dpi = dpi,
                Mono = body.Mono ?? false,
            });
        }
        catch (LabelRenderException ex)
        {
            metrics.PreviewFinished(template.Code ?? "", dpi, ex.ErrorCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return AdminResults.Invalid($"渲染失败（{ex.ErrorCode}）：{ex.Message}");
        }

        metrics.PreviewFinished(template.Code ?? "", dpi, "ok", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        var headers = http.Response.Headers;
        headers[HeaderNames.LabelDpi] = dpi.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.ServerTiming] = result.Timings.ToServerTimingHeader();
        if (result.Warnings.Count > 0)
        {
            headers[HeaderNames.LabelWarnings] = string.Join(",", result.Warnings);
        }

        return Results.Bytes(result.Content, result.ContentType);
    }

    /// <summary>
    /// JSON 解析错误的位置：JSON 路径和行列（行列从 1 开始），方便在设计器里定位。
    /// </summary>
    internal static string JsonErrorMessage(JsonException ex)
    {
        var path = string.IsNullOrEmpty(ex.Path) ? "$" : ex.Path;
        var position = ex.LineNumber is { } line ? $"，第 {line + 1} 行第 {(ex.BytePositionInLine ?? 0) + 1} 个字节" : "";
        return $"模板 JSON 不合法，位置 {path}{position}";
    }

    /// <summary>预览请求。</summary>
    /// <param name="Template">模板 JSON（可以是还没保存的草稿）。</param>
    /// <param name="Data">字段数据；不传时用字段的样例值。</param>
    /// <param name="Dpi">203、300、600；不传时用模板的 DPI。</param>
    /// <param name="Mono">true 时输出黑白图（热敏打印效果）。</param>
    internal sealed record PreviewRequest(LabelTemplate? Template, Dictionary<string, JsonElement>? Data, int? Dpi, bool? Mono);
}
