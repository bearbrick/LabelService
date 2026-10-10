using LabelService.Application.Identity;
using LabelService.Application.Layouts.Dtos;
using LabelService.Application.Logs;
using LabelService.Application.Rendering.Dtos;
using LabelService.Application.Shared;
using LabelService.Application.Templates;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Domain.Layouts;
using LabelService.Domain.Shared;
using LabelService.Rendering;
using LabelService.Rendering.Validation;

namespace LabelService.Application.Layouts;

/// <summary>
/// 拼版版式的应用服务实现。
/// </summary>
/// <param name="layouts">版式仓储（只读查询）。</param>
/// <param name="manager">版式的核心流程。</param>
/// <param name="templates">已发布模板的内存缓存（预览用）。</param>
/// <param name="renderer">渲染引擎。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="currentAdmin">当前用户。</param>
public sealed class SheetLayoutAppService(
    ISheetLayoutRepository layouts,
    SheetLayoutManager manager,
    PublishedTemplateCache templates,
    ILabelRenderer renderer,
    IOperationLogger operationLog,
    ICurrentAdmin currentAdmin) : ISheetLayoutAppService
{
    /// <summary>
    /// 格式或 DPI 不支持时的说明。
    /// </summary>
    private static readonly string FormatRule = "format 只能是 png、pdf；dpi 只能是 " + string.Join("、", RenderLimits.AllowedDpi);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SheetLayoutDto>> ListAsync(double? slotWidth, double? slotHeight)
    {
        var items = (await layouts.ListAsync()).Select(ToDto);
        if (slotWidth is { } width && slotHeight is { } height)
        {
            items = items.Where(item => SheetLayoutRules.Fits(item.Layout, width, height));
        }

        return items.ToList();
    }

    /// <inheritdoc />
    public async Task<SheetLayoutDto> GetAsync(string code) =>
        await layouts.FindAsync(code) is { } entity ? ToDto(entity) : throw AdminErrors.NotFoundError($"版式 {code} 不存在");

    /// <inheritdoc />
    public Task<SheetLayoutDto> CreateAsync(SaveSheetLayoutDto input) => SaveAsync(null, input);

    /// <inheritdoc />
    public Task<SheetLayoutDto> UpdateAsync(string code, SaveSheetLayoutDto input) => SaveAsync(code, input);

    /// <inheritdoc />
    public async Task DeleteAsync(string code)
    {
        if (!await manager.DeleteAsync(code))
        {
            throw AdminErrors.NotFoundError($"版式 {code} 不存在");
        }

        await operationLog.WriteAsync("拼版版式", code, "删除");
    }

    /// <inheritdoc />
    public RenderedLabelDto Preview(SheetLayoutPreviewInput input)
    {
        var layout = CheckLayout(input.Layout);
        if (!TryFormat(input.Format, out var format) || !TryDpi(input.Dpi, out var dpi))
        {
            throw new UserFriendlyException(FormatRule);
        }

        if (string.IsNullOrWhiteSpace(input.TemplateCode) || !templates.TryGetPublished(input.TemplateCode, input.Version, out var published))
        {
            throw AdminErrors.NotFoundError($"模板 {input.TemplateCode} 不存在或还没有发布");
        }

        if (!currentAdmin.Principal.InScope(published.BusinessId))
        {
            throw AdminErrors.ForbiddenError("这个模板不在你的业务范围内");
        }

        var template = published.Template;
        if (!SheetLayoutRules.Fits(layout, template.Page.Width, template.Page.Height))
        {
            throw new UserFriendlyException($"模板 {template.Code} 的尺寸和版式的格子尺寸不一致");
        }

        var startSlot = input.StartSlot ?? 1;
        if (startSlot < 1 || startSlot > layout.Slots.Count)
        {
            throw new UserFriendlyException($"startSlot 应在 1–{layout.Slots.Count}");
        }

        // 没给数据时用样例值把一页剩下的格子铺满。
        var items = input.Items is { Count: > 0 }
            ? input.Items
            : [new SheetLayoutPreviewItem(TemplateSampleData.Create(template.Fields, []), layout.Slots.Count - startSlot + 1)];
        if (items.Sum(item => item.Copies ?? 1) > RenderLimits.MaxBatchLabels || items.Any(item => item.Copies is < 1))
        {
            throw new UserFriendlyException($"份数至少 1，总张数不超过 {RenderLimits.MaxBatchLabels}");
        }

        // 逐条校验数据，收集全部问题一次报出。
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
            throw new UserFriendlyException("数据不合法：" + errors[0], errors);
        }

        return Render(new RenderJob { Template = template, Labels = labels, Format = format, Dpi = dpi, Layout = layout, StartSlot = startSlot });
    }

    /// <inheritdoc />
    public RenderedLabelDto TestPage(SheetLayoutTestPageInput input)
    {
        var layout = CheckLayout(input.Layout);
        if (!TryFormat(input.Format ?? "pdf", out var format) || !TryDpi(input.Dpi, out var dpi))
        {
            throw new UserFriendlyException(FormatRule);
        }

        return Render(LayoutTestPageBuilder.Job(layout, dpi, format));
    }

    /// <summary>
    /// 转成接口返回的版式。
    /// </summary>
    /// <param name="entity">版式记录。</param>
    /// <returns>版式 DTO。</returns>
    private static SheetLayoutDto ToDto(SheetLayoutEntity entity) =>
        new(SheetLayoutMapping.ToLayout(entity), entity.Enabled, entity.UpdatedBy, entity.UpdatedAt);

    /// <summary>
    /// 规范化并检查请求里的版式（预览、测试页用，版式不保存）。
    /// </summary>
    /// <param name="layout">请求里的版式。</param>
    /// <returns>规范化后的版式。</returns>
    /// <exception cref="UserFriendlyException">缺少版式、检查不通过。</exception>
    private static SheetLayout CheckLayout(SheetLayout? layout)
    {
        if (layout is null)
        {
            throw new UserFriendlyException("请求体缺少 layout（版式 JSON）");
        }

        var normalized = SheetLayoutRules.Normalize(layout);
        var problems = SheetLayoutRules.Validate(normalized);
        return problems.Count > 0 ? throw new UserFriendlyException("版式检查不通过：" + problems[0], problems) : normalized;
    }

    /// <summary>
    /// 解析输出格式：版式只支持 PNG 和 PDF。
    /// </summary>
    /// <param name="value">请求里的格式；null 时按 png。</param>
    /// <param name="format">解析出的格式。</param>
    /// <returns>支持时为 true。</returns>
    private static bool TryFormat(string? value, out LabelFormat format) =>
        LabelFormats.TryParse(value ?? "png", out format) && format is LabelFormat.Png or LabelFormat.Pdf;

    /// <summary>
    /// 解析分辨率。
    /// </summary>
    /// <param name="value">请求里的 DPI；null 时用默认值。</param>
    /// <param name="dpi">实际使用的 DPI。</param>
    /// <returns>支持时为 true。</returns>
    private static bool TryDpi(int? value, out int dpi)
    {
        dpi = value ?? RenderLimits.DefaultDpi;
        return RenderLimits.AllowedDpi.Contains(dpi);
    }

    /// <summary>
    /// 新建或修改（共用）：检查请求、保存、记操作日志。
    /// </summary>
    /// <param name="code">修改时的版式编码；新建时为 null。</param>
    /// <param name="input">请求内容。</param>
    /// <returns>保存后的版式。</returns>
    /// <exception cref="UserFriendlyException">缺少版式、改了编码、检查不通过、新建时编码已存在、修改时不存在（NOT_FOUND）。</exception>
    private async Task<SheetLayoutDto> SaveAsync(string? code, SaveSheetLayoutDto input)
    {
        if (input.Layout is null)
        {
            throw new UserFriendlyException("请求体缺少 layout（版式 JSON）");
        }

        if (code is not null && input.Layout.Code != code)
        {
            throw new UserFriendlyException("版式编码不能修改；要换编码请新建版式");
        }

        var entity = await manager.SaveAsync(input.Layout, input.Enabled ?? true, currentAdmin.Principal.User.Account, create: code is null)
            ?? throw AdminErrors.NotFoundError($"版式 {code} 不存在");
        var dto = ToDto(entity);
        var action = code is null ? "新建" : "修改";
        await operationLog.WriteAsync("拼版版式", entity.Code, $"{action}，{entity.Name}，{dto.Layout.Slots.Count} 个格子{(entity.Enabled ? "" : "，已停用")}");
        return dto;
    }

    /// <summary>
    /// 渲染拼版文件。
    /// </summary>
    /// <param name="job">渲染任务（带版式）。</param>
    /// <returns>文件和响应头要用的张数、页数、DPI、各阶段耗时、提示。</returns>
    /// <exception cref="UserFriendlyException">渲染失败。</exception>
    private RenderedLabelDto Render(RenderJob job)
    {
        RenderResult result;
        try
        {
            result = renderer.Render(job);
        }
        catch (LabelRenderException ex)
        {
            throw new UserFriendlyException($"渲染失败（{ex.ErrorCode}）：{ex.Message}");
        }

        return new RenderedLabelDto(result.Content, result.ContentType, $"{job.Layout!.Code}.{result.Extension}", result.Timings.ToServerTimingHeader(), result.Warnings)
        {
            LabelCount = result.LabelCount,
            PageCount = result.PageCount ?? 1,
            Dpi = result.Dpi,
        };
    }
}
