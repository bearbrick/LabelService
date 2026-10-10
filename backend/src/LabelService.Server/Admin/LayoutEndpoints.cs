using System.Globalization;
using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using LabelService.Server.Layouts;
using LabelService.Server.Templates;

namespace LabelService.Server.Admin;

/// <summary>
/// 拼版版式（ADR 0005，T-018）：查看登录即可，增删改要 res.manage；预览和打印校准测试页给拼版设计器用（T-019）。
/// </summary>
/// <remarks>
/// 版式 JSON 规范见 docs/contracts.md“拼版”。保存后立即更新内存快照，开放接口马上能用。
/// </remarks>
internal static class LayoutEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/layouts", ListAsync).RequireAdmin();
        api.MapGet("/layouts/{code}", GetAsync).RequireAdmin();
        api.MapPost("/layouts", (LayoutBody body, HttpContext http, LayoutRepository repository, OperationLog log) => SaveAsync(null, body, http, repository, log))
            .RequireAdmin(PermissionNames.ResourceManage);
        api.MapPut("/layouts/{code}", (string code, LayoutBody body, HttpContext http, LayoutRepository repository, OperationLog log) => SaveAsync(code, body, http, repository, log))
            .RequireAdmin(PermissionNames.ResourceManage);
        api.MapDelete("/layouts/{code}", DeleteAsync).RequireAdmin(PermissionNames.ResourceManage);
        api.MapPost("/layouts/preview", Preview).RequireAdmin(PermissionNames.TemplateView);
        api.MapPost("/layouts/test-page", TestPage).RequireAdmin();
    }

    /// <summary>
    /// 版式列表；带 slotWidth、slotHeight 时只列格子尺寸相同的（模板详情页“可用的版式”）。
    /// </summary>
    private static async Task<IResult> ListAsync(double? slotWidth, double? slotHeight, LayoutRepository repository)
    {
        var records = await repository.ListAsync();
        var items = records.Select(View);
        if (slotWidth is { } width && slotHeight is { } height)
        {
            items = items.Where(item => LayoutValidator.Fits(item.Layout, width, height));
        }

        return Results.Json(items.ToList(), LabelJson.Options);
    }

    private static async Task<IResult> GetAsync(string code, LayoutRepository repository) =>
        await repository.FindAsync(code) is { } record ? Results.Json(View(record), LabelJson.Options) : AdminResults.NotFound($"版式 {code} 不存在");

    private static async Task<IResult> SaveAsync(string? code, LayoutBody body, HttpContext http, LayoutRepository repository, OperationLog log)
    {
        if (body.Layout is null)
        {
            return AdminResults.Invalid("请求体缺少 layout（版式 JSON）");
        }

        if (code is not null && body.Layout.Code != code)
        {
            return AdminResults.Invalid("版式编码不能修改；要换编码请新建版式");
        }

        try
        {
            var record = await repository.SaveAsync(body.Layout, body.Enabled ?? true, http.CurrentAdmin().User.Account, create: code is null);
            if (record is null)
            {
                return AdminResults.NotFound($"版式 {code} 不存在");
            }

            await log.WriteAsync(http, "拼版版式", record.Code, (code is null ? "新建" : "修改") + $"，{record.Name}，{SlotCount(record)} 个格子{(record.Enabled ? "" : "，已停用")}");
            return Results.Json(View(record), LabelJson.Options);
        }
        catch (UserFriendlyException ex)
        {
            return AdminResults.From(ex);
        }
    }

    private static async Task<IResult> DeleteAsync(string code, HttpContext http, LayoutRepository repository, OperationLog log)
    {
        if (!await repository.DeleteAsync(code))
        {
            return AdminResults.NotFound($"版式 {code} 不存在");
        }

        await log.WriteAsync(http, "拼版版式", code, "删除");
        return Results.NoContent();
    }

    /// <summary>
    /// 预览：还没保存的版式 + 已发布的模板 + 数据（不传时用样例值铺满一页），返回 PNG（多页时 ZIP）或 PDF。
    /// </summary>
    private static IResult Preview(LayoutPreviewBody body, HttpContext http, ITemplateStore templates, ILabelRenderer renderer)
    {
        if (CheckLayout(body.Layout, out var layout) is { } problem)
        {
            return problem;
        }

        if (!TryFormat(body.Format, out var format) || !TryDpi(body.Dpi, out var dpi))
        {
            return AdminResults.Invalid("format 只能是 png、pdf；dpi 只能是 " + string.Join("、", RenderLimits.AllowedDpi));
        }

        if (string.IsNullOrWhiteSpace(body.TemplateCode) || !templates.TryGetPublished(body.TemplateCode, body.Version, out var published))
        {
            return AdminResults.NotFound($"模板 {body.TemplateCode} 不存在或还没有发布");
        }

        if (!http.CurrentAdmin().InScope(published.BusinessId))
        {
            return AdminResults.Forbidden("这个模板不在你的业务范围内");
        }

        var template = published.Template;
        if (!LayoutValidator.Fits(layout, template.Page.Width, template.Page.Height))
        {
            return AdminResults.Invalid($"模板 {template.Code} 的尺寸和版式的格子尺寸不一致");
        }

        var startSlot = body.StartSlot ?? 1;
        if (startSlot < 1 || startSlot > layout.Slots.Count)
        {
            return AdminResults.Invalid($"startSlot 应在 1–{layout.Slots.Count}");
        }

        // 没给数据时用样例值把一页剩下的格子铺满。
        var items = body.Items is { Count: > 0 }
            ? body.Items
            : [new LayoutPreviewItem(TemplatePublishValidator.SampleData(template.Fields, []), layout.Slots.Count - startSlot + 1)];
        if (items.Sum(item => item.Copies ?? 1) > RenderLimits.MaxBatchLabels || items.Any(item => item.Copies is < 1))
        {
            return AdminResults.Invalid($"份数至少 1，总张数不超过 {RenderLimits.MaxBatchLabels}");
        }

        var labels = new List<LabelInstance>(items.Count);
        var errors = new List<string>();
        for (var index = 0; index < items.Count; index++)
        {
            var validation = FieldValidator.Validate(template.Fields, null, items[index].Data ?? [], index);
            errors.AddRange(validation.Errors.Select(error => $"{error.Code}：{error.Message}"));
            labels.Add(new LabelInstance(validation.Values, items[index].Copies ?? 1));
        }

        if (errors.Count > 0)
        {
            return AdminResults.Invalid("数据不合法：" + errors[0], errors);
        }

        return Render(http, renderer, new RenderJob { Template = template, Labels = labels, Format = format, Dpi = dpi, Layout = layout, StartSlot = startSlot });
    }

    /// <summary>
    /// 打印校准测试页：每个格子画边框、中心十字和序号，默认 PDF。
    /// </summary>
    private static IResult TestPage(LayoutTestPageBody body, HttpContext http, ILabelRenderer renderer)
    {
        if (CheckLayout(body.Layout, out var layout) is { } problem)
        {
            return problem;
        }

        if (!TryFormat(body.Format ?? "pdf", out var format) || !TryDpi(body.Dpi, out var dpi))
        {
            return AdminResults.Invalid("format 只能是 png、pdf；dpi 只能是 " + string.Join("、", RenderLimits.AllowedDpi));
        }

        return Render(http, renderer, LayoutTestPage.Job(layout, dpi, format));
    }

    private static IResult Render(HttpContext http, ILabelRenderer renderer, RenderJob job)
    {
        RenderResult result;
        try
        {
            result = renderer.Render(job);
        }
        catch (LabelRenderException ex)
        {
            return AdminResults.Invalid($"渲染失败（{ex.ErrorCode}）：{ex.Message}");
        }

        var headers = http.Response.Headers;
        headers[HeaderNames.LabelCount] = result.LabelCount.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.PageCount] = (result.PageCount ?? 1).ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.LabelDpi] = result.Dpi.ToString(CultureInfo.InvariantCulture);
        headers[HeaderNames.ServerTiming] = result.Timings.ToServerTimingHeader();
        if (result.Warnings.Count > 0)
        {
            headers[HeaderNames.LabelWarnings] = string.Join(",", result.Warnings);
        }

        return Results.Bytes(result.Content, result.ContentType, $"{job.Layout!.Code}.{result.Extension}");
    }

    private static IResult? CheckLayout(SheetLayout? layout, out SheetLayout normalized)
    {
        normalized = null!;
        if (layout is null)
        {
            return AdminResults.Invalid("请求体缺少 layout（版式 JSON）");
        }

        normalized = LayoutRepository.Normalize(layout);
        var problems = LayoutValidator.Validate(normalized);
        return problems.Count > 0 ? AdminResults.Invalid("版式检查不通过：" + problems[0], problems) : null;
    }

    private static bool TryFormat(string? value, out LabelFormat format) =>
        LabelFormats.TryParse(value ?? "png", out format) && format is LabelFormat.Png or LabelFormat.Pdf;

    private static bool TryDpi(int? value, out int dpi)
    {
        dpi = value ?? RenderLimits.DefaultDpi;
        return RenderLimits.AllowedDpi.Contains(dpi);
    }

    private static int SlotCount(SheetLayoutEntity record) => LayoutRepository.ToModel(record).Slots.Count;

    private static LayoutView View(SheetLayoutEntity record) => new(LayoutRepository.ToModel(record), record.Enabled, record.UpdatedBy, record.UpdatedAt);

    /// <summary>列表和详情的一项：版式 JSON 加上启用状态和修改记录。</summary>
    internal sealed record LayoutView(SheetLayout Layout, bool Enabled, string UpdatedBy, DateTime UpdatedAt);

    /// <summary>新建、修改的请求体。</summary>
    /// <param name="Layout">版式 JSON。</param>
    /// <param name="Enabled">是否启用，默认启用。</param>
    internal sealed record LayoutBody(SheetLayout? Layout, bool? Enabled);

    /// <summary>预览的请求体。</summary>
    internal sealed record LayoutPreviewBody(SheetLayout? Layout, string? TemplateCode, int? Version, string? Format, int? Dpi, int? StartSlot, List<LayoutPreviewItem>? Items);

    /// <summary>预览的一条数据。</summary>
    internal sealed record LayoutPreviewItem(Dictionary<string, JsonElement>? Data, int? Copies);

    /// <summary>测试页的请求体。</summary>
    internal sealed record LayoutTestPageBody(SheetLayout? Layout, string? Format, int? Dpi);
}
