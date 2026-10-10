using System.Globalization;
using LabelService.Application.Layouts;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Domain.Layouts;

namespace LabelService.Application.OpenApi;

/// <summary>
/// 单张和批量接口共用的拼版参数检查（docs/contracts.md“拼版”）。不传 layout 时什么都不做。
/// </summary>
internal static class SheetParameters
{
    /// <summary>
    /// 版式编码在日志里最多保留几个字符。
    /// </summary>
    private const int MaxLoggedCodeLength = 64;

    /// <summary>
    /// 取模板之前就能做的检查：ZPL 不能拼版、startSlot 从 1 开始。没传 layout 时 startSlot 不起作用。
    /// </summary>
    /// <param name="trace">调用记录，记下版式编码。</param>
    /// <param name="layoutCode">请求里的版式编码。</param>
    /// <param name="startSlot">请求里的起始格子。</param>
    /// <param name="format">输出格式。</param>
    /// <returns>有问题时返回错误，否则 null。</returns>
    public static OpenApiError? Check(RenderTrace trace, string? layoutCode, int? startSlot, LabelFormat format)
    {
        if (string.IsNullOrEmpty(layoutCode))
        {
            return null;
        }

        trace.LayoutCode = layoutCode.Length > MaxLoggedCodeLength ? layoutCode[..MaxLoggedCodeLength] : layoutCode;
        if (format == LabelFormat.Zpl)
        {
            return new OpenApiError(ErrorCodes.FormatInvalid, "ZPL 面向热敏标签机，不支持拼版；layout 只能和 pdf、png 一起用");
        }

        return startSlot is < 1 ? new OpenApiError(ErrorCodes.StartSlotInvalid, "startSlot 从 1 开始") : null;
    }

    /// <summary>
    /// 取到模板之后：版式存在且启用、格子尺寸和模板一致、startSlot 不超过格子数。
    /// </summary>
    /// <param name="layouts">版式的内存缓存。</param>
    /// <param name="layoutCode">请求里的版式编码。</param>
    /// <param name="startSlot">请求里的起始格子。</param>
    /// <param name="template">要用的模板。</param>
    /// <param name="layout">找到的版式；没传 layout 或有问题时为 null。</param>
    /// <returns>有问题时返回错误，否则 null。</returns>
    public static OpenApiError? Resolve(SheetLayoutCache layouts, string? layoutCode, int? startSlot, LabelTemplate template, out SheetLayout? layout)
    {
        layout = null;
        if (string.IsNullOrEmpty(layoutCode))
        {
            return null;
        }

        if (!layouts.TryGet(layoutCode, out var found))
        {
            return new OpenApiError(ErrorCodes.LayoutNotFound, $"拼版版式 {layoutCode} 不存在或已停用");
        }

        if (!SheetLayoutRules.Fits(found, template.Page.Width, template.Page.Height))
        {
            return new OpenApiError(
                ErrorCodes.LayoutSizeMismatch,
                $"模板 {template.Code} 是 {Mm(template.Page.Width)}×{Mm(template.Page.Height)} mm，版式 {found.Code} 的格子是 {Mm(found.Slot.Width)}×{Mm(found.Slot.Height)} mm");
        }

        if (startSlot > found.Slots.Count)
        {
            return new OpenApiError(ErrorCodes.StartSlotInvalid, $"版式 {found.Code} 只有 {found.Slots.Count} 个格子，startSlot 应在 1–{found.Slots.Count}");
        }

        layout = found;
        return null;
    }

    /// <summary>
    /// 长度写成最多两位小数，不带多余的 0。
    /// </summary>
    /// <param name="value">长度，mm。</param>
    /// <returns>如 “100”、“62.5”。</returns>
    private static string Mm(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
