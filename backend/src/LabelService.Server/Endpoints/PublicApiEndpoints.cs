using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using LabelService.Server.Auth;
using LabelService.Server.Diagnostics;
using LabelService.Server.Templates;

namespace LabelService.Server.Endpoints;

/// <summary>
/// 开放接口 /api/v1：业务系统和客户端 SDK 调用。请求、响应格式见 docs/contracts.md。
/// </summary>
internal static class PublicApiEndpoints
{
    /// <summary>
    /// 注册开放接口路由。
    /// </summary>
    public static IEndpointRouteBuilder MapPublicApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapPost("/render", RenderAsync);
        api.MapGet("/templates/{code}/schema", GetSchema);
        api.MapPost("/render/batch", (HttpContext http) =>
            ApiErrors.Create(http, ApiErrors.NotImplemented, "批量生成尚未实现（T-007）", statusCode: 501));
        api.MapPost("/preview", (HttpContext http) =>
            ApiErrors.Create(http, ApiErrors.NotImplemented, "服务端预览尚未实现（T-016）", statusCode: 501));
        return app;
    }

    /// <summary>
    /// POST /api/v1/render：单张生成，成功时直接返回文件流。
    /// </summary>
    private static async Task<IResult> RenderAsync(
        HttpContext http,
        ApiKeyAuthenticator authenticator,
        ITemplateStore store,
        ILabelRenderer renderer,
        LabelMetrics metrics,
        ILogger<RenderCall> logger)
    {
        using var call = new RenderCall(http, metrics, logger, "render");
        var client = authenticator.Authenticate(http.Request.Headers[HeaderNames.ApiKey]);
        var stage = call.Timings.Mark(TimingStages.Auth, call.Started);
        if (client is null)
        {
            return call.Fail(ErrorCodes.Unauthorized, "API Key 缺失或无效");
        }

        call.Client = client.Name;
        var (body, readError) = await ReadJsonAsync<RenderApiRequest>(http);
        if (readError is not null)
        {
            return call.Fail(readError.Value.Code, readError.Value.Message, statusCode: readError.Value.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body?.TemplateCode) || body.Data is null)
        {
            return call.Fail(ErrorCodes.InvalidJson, "请求体缺少 templateCode 或 data");
        }

        call.TemplateCode = body.TemplateCode;
        if (!LabelFormats.TryParse(body.Format, out var format))
        {
            return call.Fail(ErrorCodes.FormatInvalid, "format 只能是 pdf、png、zpl");
        }

        call.Format = format;
        if (body.Dpi is { } requestedDpi && !RenderLimits.AllowedDpi.Contains(requestedDpi))
        {
            return call.Fail(ErrorCodes.DpiInvalid, "dpi 只能是 203、300、600");
        }

        var copies = body.Copies ?? 1;
        if (copies is < RenderLimits.MinCopies or > RenderLimits.MaxCopies)
        {
            return call.Fail(ErrorCodes.CopiesInvalid, $"copies 应在 {RenderLimits.MinCopies}–{RenderLimits.MaxCopies}");
        }

        if (format == LabelFormat.Png && copies != 1)
        {
            return call.Fail(ErrorCodes.CopiesInvalid, "PNG 单张只能输出 1 份；多份请用批量接口并选 zip");
        }

        if (!renderer.Supports(format))
        {
            return call.Fail(ApiErrors.NotImplemented, $"输出格式 {body.Format} 尚未实现（PDF 见 T-005，ZPL 见 T-006）", statusCode: 501);
        }

        stage = call.Timings.Mark(TimingStages.Validate, stage);
        if (!store.TryGetPublished(body.TemplateCode, body.Version, out var published))
        {
            return call.Fail(ErrorCodes.TemplateNotFound, $"模板 {body.TemplateCode} 不存在、版本不存在或还没有发布");
        }

        if (!client.CanUse(body.TemplateCode))
        {
            return call.Fail(ErrorCodes.TemplateForbidden, $"调用方 {client.Name} 无权使用模板 {body.TemplateCode}");
        }

        stage = call.Timings.Mark(TimingStages.Template, stage);
        var validation = FieldValidator.Validate(published.Template.Fields, null, body.Data);
        call.Timings.Mark(TimingStages.Validate, stage);
        if (!validation.IsValid)
        {
            var first = validation.Errors[0];
            return call.Fail(first.Code, first.Message ?? first.Code, validation.Errors);
        }

        call.Dpi = body.Dpi ?? (published.Template.Page.Dpi > 0 ? published.Template.Page.Dpi : RenderLimits.DefaultDpi);
        var job = new RenderJob
        {
            Template = published.Template,
            Labels = [new LabelInstance(validation.Values, copies)],
            Format = format,
            Dpi = call.Dpi,
            Mono = body.Mono ?? false,
            Timings = call.Timings,
        };

        try
        {
            return call.Succeed(renderer.Render(job), published.Version);
        }
        catch (LabelRenderException ex)
        {
            return call.Fail(ex.ErrorCode, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "渲染失败 template={Template} requestId={RequestId}", call.TemplateCode, call.RequestId);
            return call.Fail(ErrorCodes.RenderFailed, "渲染内部错误，请凭 traceId 联系标签服务管理员");
        }
    }

    /// <summary>
    /// GET /api/v1/templates/{code}/schema：最新发布版本的字段定义和调用样例。
    /// </summary>
    private static IResult GetSchema(string code, HttpContext http, ApiKeyAuthenticator authenticator, ITemplateStore store)
    {
        var client = authenticator.Authenticate(http.Request.Headers[HeaderNames.ApiKey]);
        if (client is null)
        {
            return ApiErrors.Create(http, ErrorCodes.Unauthorized, "API Key 缺失或无效");
        }

        if (!store.TryGetPublished(code, null, out var published))
        {
            return ApiErrors.Create(http, ErrorCodes.TemplateNotFound, $"模板 {code} 不存在或还没有发布");
        }

        if (!client.CanUse(code))
        {
            return ApiErrors.Create(http, ErrorCodes.TemplateForbidden, $"调用方 {client.Name} 无权使用模板 {code}");
        }

        var template = published.Template;
        var response = new TemplateSchemaResponse
        {
            TemplateCode = template.Code,
            Name = template.Name,
            Version = published.Version,
            Page = template.Page,
            Fields = template.Fields.Select(field => new FieldDefinition
            {
                Key = field.Key,
                Name = field.Name,
                Type = field.Type,
                Required = field.Required,
                Default = field.Default,
                Format = field.Format,
                MaxLength = field.MaxLength,
            }).ToList(),
            Sample = template.Fields
                .Where(field => field.Sample is not null)
                .ToDictionary(field => field.Key, field => field.Sample!.Value),
        };
        return Results.Json(response, LabelJson.Options);
    }

    private static async Task<(T? Body, (string Code, string Message, int StatusCode)? Error)> ReadJsonAsync<T>(HttpContext http)
        where T : class
    {
        try
        {
            var body = await JsonSerializer.DeserializeAsync<T>(http.Request.Body, LabelJson.Options, http.RequestAborted);
            return (body, null);
        }
        catch (JsonException ex)
        {
            return (null, (ErrorCodes.InvalidJson, $"请求体不是合法 JSON：{ex.Message}", 400));
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, (ErrorCodes.InvalidJson, $"请求体超过 {RenderLimits.MaxRequestBodyBytes / 1024 / 1024} MB", 413));
        }
    }
}
