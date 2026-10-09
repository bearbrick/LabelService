using System.Globalization;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Server.Layouts;

namespace LabelService.Server.Endpoints;

/// <summary>
/// 单张和批量接口共用的拼版参数检查（docs/contracts.md“拼版”）。不传 layout 时什么都不做，行为和原来一样。
/// </summary>
internal static class SheetParameters
{
    /// <summary>
    /// 取模板之前就能做的检查：ZPL 不能拼版、startSlot 从 1 开始。没传 layout 时 startSlot 不起作用。
    /// </summary>
    /// <returns>有问题时返回错误响应。</returns>
    public static IResult? Check(RenderCall call, string? layoutCode, int? startSlot, LabelFormat format)
    {
        if (string.IsNullOrEmpty(layoutCode))
        {
            return null;
        }

        call.LayoutCode = layoutCode.Length > 64 ? layoutCode[..64] : layoutCode;
        if (format == LabelFormat.Zpl)
        {
            return call.Fail(ErrorCodes.FormatInvalid, "ZPL 面向热敏标签机，不支持拼版；layout 只能和 pdf、png 一起用");
        }

        return startSlot is < 1 ? call.Fail(ErrorCodes.StartSlotInvalid, "startSlot 从 1 开始") : null;
    }

    /// <summary>
    /// 取到模板之后：版式存在且启用、格子尺寸和模板一致、startSlot 不超过格子数。
    /// </summary>
    /// <returns>没传 layout 时两项都是 null；有问题时 Error 不为 null。</returns>
    public static (SheetLayout? Layout, IResult? Error) Resolve(RenderCall call, LayoutStore layouts, string? layoutCode, int? startSlot, LabelTemplate template)
    {
        if (string.IsNullOrEmpty(layoutCode))
        {
            return (null, null);
        }

        if (!layouts.TryGet(layoutCode, out var layout))
        {
            return (null, call.Fail(ErrorCodes.LayoutNotFound, $"拼版版式 {layoutCode} 不存在或已停用"));
        }

        if (!LayoutValidator.Fits(layout, template.Page.Width, template.Page.Height))
        {
            return (null, call.Fail(ErrorCodes.LayoutSizeMismatch,
                $"模板 {template.Code} 是 {Mm(template.Page.Width)}×{Mm(template.Page.Height)} mm，版式 {layout.Code} 的格子是 {Mm(layout.Slot.Width)}×{Mm(layout.Slot.Height)} mm"));
        }

        if (startSlot > layout.Slots.Count)
        {
            return (null, call.Fail(ErrorCodes.StartSlotInvalid, $"版式 {layout.Code} 只有 {layout.Slots.Count} 个格子，startSlot 应在 1–{layout.Slots.Count}"));
        }

        return (layout, null);
    }

    private static string Mm(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
