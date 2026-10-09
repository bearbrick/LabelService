using System.Globalization;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using LabelService.Server.Templates;

namespace LabelService.Server.Admin;

/// <summary>
/// 接口调试（api.test）：在后台用已发布的模板试一次生成，请求体和开放接口一样，不需要 API Key。
/// </summary>
/// <remarks>
/// 管理接口和开放接口的鉴权互不相通（ADR 0006）：这里只认登录令牌，按用户的业务范围限制模板，不写调用日志。
/// </remarks>
internal static class ApiTestEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api) =>
        api.MapPost("/api-test/render", RenderAsync).RequireAdmin(Permissions.ApiTest);

    private static async Task<IResult> RenderAsync(RenderApiRequest body, HttpContext http, ITemplateStore store, ILabelRenderer renderer)
    {
        if (string.IsNullOrWhiteSpace(body.TemplateCode) || !store.TryGetPublished(body.TemplateCode, body.Version, out var published)
            || !http.CurrentAdmin().InScope(published.BusinessId))
        {
            return AdminResults.NotFound($"模板 {body.TemplateCode} 不存在、版本不存在或还没有发布");
        }

        if (!LabelFormats.TryParse(body.Format, out var format) || !renderer.Supports(format))
        {
            return AdminResults.Invalid("format 只能是 pdf、png、zpl");
        }

        if (body.Dpi is { } dpi && !RenderLimits.AllowedDpi.Contains(dpi))
        {
            return AdminResults.Invalid("dpi 只能是 203、300、600");
        }

        var copies = Math.Clamp(body.Copies ?? 1, RenderLimits.MinCopies, RenderLimits.MaxCopies);
        if (format == LabelFormat.Png)
        {
            copies = 1;
        }

        var validation = FieldValidator.Validate(published.Template.Fields, null, body.Data ?? []);
        if (!validation.IsValid)
        {
            return AdminResults.Invalid(validation.Errors[0].Message ?? validation.Errors[0].Code, validation.Errors.Select(error => $"{error.Code}：{error.Message}").ToArray());
        }

        try
        {
            var result = renderer.Render(new RenderJob
            {
                Template = published.Template,
                Labels = [new LabelInstance(validation.Values, copies)],
                Format = format,
                Dpi = body.Dpi ?? (published.Template.Page.Dpi > 0 ? published.Template.Page.Dpi : RenderLimits.DefaultDpi),
                Mono = body.Mono ?? false,
            });
            var headers = http.Response.Headers;
            headers[HeaderNames.TemplateVersion] = published.Version.ToString(CultureInfo.InvariantCulture);
            headers[HeaderNames.LabelCount] = result.LabelCount.ToString(CultureInfo.InvariantCulture);
            headers[HeaderNames.ServerTiming] = result.Timings.ToServerTimingHeader();
            if (result.Warnings.Count > 0)
            {
                headers[HeaderNames.LabelWarnings] = string.Join(",", result.Warnings);
            }

            return Results.Bytes(result.Content, result.ContentType, $"{published.Template.Code}_test.{LabelFormats.Extension(format)}");
        }
        catch (LabelRenderException ex)
        {
            return AdminResults.Invalid($"{ex.ErrorCode}：{ex.Message}");
        }
    }
}

/// <summary>
/// 管理接口 /api/admin 的路由注册（只给设计器和管理后台用，不对外公开）。
/// </summary>
internal static class AdminEndpoints
{
    /// <summary>
    /// 注册全部管理接口。
    /// </summary>
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/admin");
        AccountEndpoints.Map(api);
        UserRoleEndpoints.Map(api);
        BusinessEndpoints.Map(api);
        TemplateEndpoints.Map(api);
        ResourceEndpoints.Map(api);
        ClientEndpoints.Map(api);
        LogEndpoints.Map(api);
        ApiTestEndpoints.Map(api);
        PreviewEndpoints.Map(api);
        return app;
    }

    /// <summary>
    /// 要求登录；<paramref name="permission"/> 不为空时还要有这个权限点（必须先改密码的用户只能访问不带权限点的接口）。
    /// </summary>
    public static RouteHandlerBuilder RequireAdmin(this RouteHandlerBuilder builder, string? permission = null) =>
        builder.AddEndpointFilter(new AdminFilter(permission));
}
