using LabelService.Application.ApiClients;
using LabelService.Application.Layouts;
using LabelService.Application.Rendering;
using LabelService.Application.Templates;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Validation;

namespace LabelService.Application.OpenApi;

/// <summary>
/// 开放接口的应用服务实现：单张生成和字段定义在这个文件，批量生成在 LabelRenderAppService.Batch.cs。
/// </summary>
/// <remarks>
/// 检查顺序和错误码见 docs/contracts.md：先检查请求本身（格式、DPI、份数、拼版参数），再取模板和版式，最后校验数据并渲染。
/// 渲染前要拿到并发名额（<see cref="RenderGate"/>），排队超时返回 503。
/// </remarks>
/// <param name="templates">已发布模板的内存缓存。</param>
/// <param name="layouts">版式的内存缓存。</param>
/// <param name="renderer">渲染引擎。</param>
/// <param name="gate">渲染并发限制。</param>
/// <param name="clock">字段自动值用的当前时间。</param>
public sealed partial class LabelRenderAppService(
    PublishedTemplateCache templates,
    SheetLayoutCache layouts,
    ILabelRenderer renderer,
    RenderGate gate,
    AutoValueClock clock) : ILabelRenderAppService
{
    /// <summary>
    /// 格式不认识时的说明。
    /// </summary>
    private const string FormatRule = "format 只能是 pdf、png、zpl";

    /// <summary>
    /// DPI 不支持时的说明。
    /// </summary>
    private const string DpiRule = "dpi 只能是 203、300、600";

    /// <inheritdoc />
    public async ValueTask<OpenApiOutcome<RenderedFile>> RenderAsync(AuthenticatedClient client, RenderApiRequest? request, RenderTrace trace, CancellationToken cancellationToken)
    {
        // 第一步：只看请求本身就能发现的问题。
        if (string.IsNullOrWhiteSpace(request?.TemplateCode) || request.Data is null)
        {
            return new OpenApiError(ErrorCodes.InvalidJson, "请求体缺少 templateCode 或 data");
        }

        trace.TemplateCode = request.TemplateCode;
        if (!LabelFormats.TryParse(request.Format, out var format))
        {
            return new OpenApiError(ErrorCodes.FormatInvalid, FormatRule);
        }

        trace.Format = format;
        if (request.Dpi is { } requestedDpi && !RenderLimits.AllowedDpi.Contains(requestedDpi))
        {
            return new OpenApiError(ErrorCodes.DpiInvalid, DpiRule);
        }

        var copies = request.Copies ?? 1;
        if (copies is < RenderLimits.MinCopies or > RenderLimits.MaxCopies)
        {
            return new OpenApiError(ErrorCodes.CopiesInvalid, $"copies 应在 {RenderLimits.MinCopies}–{RenderLimits.MaxCopies}");
        }

        if (SheetParameters.Check(trace, request.Layout, request.StartSlot, format) is { } sheetError)
        {
            return sheetError;
        }

        // 拼版时份数就是铺几个格子，PNG 也可以多份。
        if (format == LabelFormat.Png && copies != 1 && string.IsNullOrEmpty(request.Layout))
        {
            return new OpenApiError(ErrorCodes.CopiesInvalid, "PNG 单张只能输出 1 份；多份请用批量接口并选 zip，或者用拼版版式");
        }

        if (!renderer.Supports(format))
        {
            return new OpenApiError(OpenApiError.NotImplemented, $"输出格式 {request.Format} 尚未实现", StatusCode: 501);
        }

        // 第二步：取模板（含授权）和版式。
        trace.Mark(TimingStages.Validate);
        if (ResolveTemplate(client, request.TemplateCode, request.Version, trace, out var published) is { } templateError)
        {
            return templateError;
        }

        if (SheetParameters.Resolve(layouts, request.Layout, request.StartSlot, published.Template, out var layout) is { } layoutError)
        {
            return layoutError;
        }

        // 第三步：校验数据，拿到并发名额后渲染。
        trace.Mark(TimingStages.Template);
        var fields = published.Template.Fields;
        var validation = FieldValidator.Validate(fields, null, request.Data, null, clock.For(fields, 1, client.Name, trace.RequestId));
        trace.Mark(TimingStages.Validate);
        if (!validation.IsValid)
        {
            var first = validation.Errors[0];
            return new OpenApiError(first.Code, first.Message ?? first.Code, validation.Errors);
        }

        trace.Dpi = request.Dpi ?? DefaultDpi(published.Template);
        var job = new RenderJob
        {
            Template = published.Template,
            Labels = [new LabelInstance(validation.Values, copies)],
            Format = format,
            Dpi = trace.Dpi,
            Mono = request.Mono ?? false,
            Layout = layout,
            StartSlot = request.StartSlot ?? 1,
            Timings = trace.Timings,
        };
        using var slot = await gate.EnterAsync(cancellationToken);
        if (slot is null)
        {
            return OpenApiError.Busy(gate.TimeoutSeconds);
        }

        try
        {
            return ToFile(renderer.Render(job), published.Version);
        }
        catch (LabelRenderException ex)
        {
            return new OpenApiError(ex.ErrorCode, ex.Message);
        }
    }

    /// <inheritdoc />
    public OpenApiOutcome<TemplateSchemaResponse> GetSchema(AuthenticatedClient client, string code)
    {
        if (!templates.TryGetPublished(code, null, out var published))
        {
            return new OpenApiError(ErrorCodes.TemplateNotFound, $"模板 {code} 不存在或还没有发布");
        }

        if (!client.CanUse(published))
        {
            return new OpenApiError(ErrorCodes.TemplateForbidden, $"调用方 {client.Name} 无权使用模板 {code}");
        }

        // 字段定义去掉设计器专用的属性（如样例值），样例值单独汇总成调用样例。
        var template = published.Template;
        return new TemplateSchemaResponse
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
                Required = field.Source != FieldSource.Template && field.Required,
                Source = field.Source,
                Default = field.Default,
                Auto = field.Auto,
                Format = field.Format,
                MaxLength = field.MaxLength,
            }).ToList(),
            // 有自动值的字段不放进样例：照着样例传反而会盖掉自动值。
            Sample = template.Fields
                .Where(field => field.Sample is not null && field.Auto is null && field.Source != FieldSource.Template)
                .ToDictionary(field => field.Key, field => field.Sample!.Value),
        };
    }

    /// <inheritdoc />
    public TemplateListResponse ListTemplates(AuthenticatedClient client) => new()
    {
        // 只看内存里的已发布模板，按调用方的授权过滤（和单张生成用同一个判断）。
        Templates = templates.LatestPublished()
            .Where(client.CanUse)
            .OrderBy(published => published.Template.Code, StringComparer.Ordinal)
            .Select(published => new TemplateSummary
            {
                TemplateCode = published.Template.Code,
                Name = published.Template.Name,
                Version = published.Version,
                Page = published.Template.Page,
            })
            .ToList(),
    };

    /// <summary>
    /// 模板没写 DPI 时用默认值。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <returns>模板的 DPI 或 <see cref="RenderLimits.DefaultDpi"/>。</returns>
    private static int DefaultDpi(LabelTemplate template) => template.Page.Dpi > 0 ? template.Page.Dpi : RenderLimits.DefaultDpi;

    /// <summary>
    /// 渲染结果转成接口返回的文件。
    /// </summary>
    /// <param name="result">渲染结果。</param>
    /// <param name="templateVersion">模板版本。</param>
    /// <returns>文件。</returns>
    private static RenderedFile ToFile(RenderResult result, int templateVersion) =>
        new(result.Content, result.ContentType, result.Extension, result.LabelCount, result.Dpi, result.Warnings, templateVersion, result.PageCount);

    /// <summary>
    /// 取已发布的模板并检查调用方有没有权限用它。
    /// </summary>
    /// <param name="client">调用方。</param>
    /// <param name="code">模板编码。</param>
    /// <param name="version">版本号；null 表示最新发布版本。</param>
    /// <param name="trace">调用记录，记下版本号。</param>
    /// <param name="published">找到的模板。</param>
    /// <returns>模板不存在或没有权限时返回错误，否则 null。</returns>
    private OpenApiError? ResolveTemplate(AuthenticatedClient client, string code, int? version, RenderTrace trace, out PublishedTemplate published)
    {
        if (!templates.TryGetPublished(code, version, out var found))
        {
            published = null!;
            return new OpenApiError(ErrorCodes.TemplateNotFound, $"模板 {code} 不存在、版本不存在或还没有发布");
        }

        published = found;
        trace.TemplateVersion = found.Version;
        return client.CanUse(found) ? null : new OpenApiError(ErrorCodes.TemplateForbidden, $"调用方 {client.Name} 无权使用模板 {code}");
    }
}
