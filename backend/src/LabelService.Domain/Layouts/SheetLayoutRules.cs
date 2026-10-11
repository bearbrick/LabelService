using System.Globalization;
using System.Text.RegularExpressions;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;

namespace LabelService.Domain.Layouts;

/// <summary>
/// 拼版版式的规则（docs/contracts.md“拼版”）：保存前的检查（编码、尺寸、旋转角度、格子不超出纸张、格子之间不重叠）、
/// 长度取整、模板尺寸和格子是否匹配。
/// </summary>
public static partial class SheetLayoutRules
{
    /// <summary>
    /// 贴边的格子允许的误差（mm）：两边都按 0.1 mm 记录，浮点误差不算重叠或越界。
    /// </summary>
    private const double Tolerance = 0.05;

    /// <summary>
    /// 检查版式，返回全部问题；没有问题时返回空列表。格子的问题带序号（从 1 开始，就是填充顺序）。
    /// </summary>
    /// <param name="layout">版式，先用 <see cref="Normalize"/> 取整。</param>
    /// <returns>全部问题。</returns>
    public static List<string> Validate(SheetLayout layout)
    {
        var problems = new List<string>();
        if (!CodePattern().IsMatch(layout.Code ?? ""))
        {
            problems.Add("版式编码要以字母开头，只能有字母、数字、_、-，最长 64 个字符");
        }

        if (string.IsNullOrWhiteSpace(layout.Name) || layout.Name.Length > 128)
        {
            problems.Add("名称不能为空，最长 128 字");
        }

        if (layout.Paper is null || layout.Slot is null || layout.Slots is null || layout.Offset is null)
        {
            problems.Add("缺少 paper、slot、slots 或 offset");
            return problems;
        }

        if (!InRange(layout.Paper.Width, RenderLimits.MaxPaperMm) || !InRange(layout.Paper.Height, RenderLimits.MaxPaperMm))
        {
            problems.Add($"纸张宽高要在 0–{RenderLimits.MaxPaperMm:0} mm 之间");
        }

        if (!InRange(layout.Slot.Width, RenderLimits.MaxPaperMm) || !InRange(layout.Slot.Height, RenderLimits.MaxPaperMm))
        {
            problems.Add("格子宽高要大于 0");
        }

        if (layout.Slots.Count is 0 or > RenderLimits.MaxLayoutSlots)
        {
            problems.Add($"格子数要在 1–{RenderLimits.MaxLayoutSlots} 之间");
        }

        if (!double.IsFinite(layout.Offset.X) || !double.IsFinite(layout.Offset.Y) || Math.Abs(layout.Offset.X) > 50 || Math.Abs(layout.Offset.Y) > 50)
        {
            problems.Add("打印偏移要在 ±50 mm 以内");
        }

        if (problems.Count > 0)
        {
            return problems;
        }

        var boxes = new List<(double Left, double Top, double Right, double Bottom)>(layout.Slots.Count);
        for (var i = 0; i < layout.Slots.Count; i++)
        {
            var slot = layout.Slots[i];
            var name = $"第 {(i + 1).ToString(CultureInfo.InvariantCulture)} 个格子";
            if (slot is null)
            {
                problems.Add(name + "是空的");
                continue;
            }

            if (!SheetLayout.AllowedRotations.Contains(slot.Rotation))
            {
                problems.Add($"{name}的旋转只能是 0、90、180、270");
                continue;
            }

            var (width, height) = layout.Footprint(slot);
            (double Left, double Top, double Right, double Bottom) box = (slot.X, slot.Y, slot.X + width, slot.Y + height);
            if (!double.IsFinite(slot.X) || !double.IsFinite(slot.Y)
                || box.Left < -Tolerance || box.Top < -Tolerance
                || box.Right > layout.Paper.Width + Tolerance || box.Bottom > layout.Paper.Height + Tolerance)
            {
                problems.Add($"{name}超出了纸张");
            }

            for (var j = 0; j < boxes.Count; j++)
            {
                var other = boxes[j];
                if (box.Left < other.Right - Tolerance && other.Left < box.Right - Tolerance
                    && box.Top < other.Bottom - Tolerance && other.Top < box.Bottom - Tolerance)
                {
                    problems.Add($"{name}和第 {(j + 1).ToString(CultureInfo.InvariantCulture)} 个格子重叠");
                }
            }

            boxes.Add(box);
        }

        return problems;
    }

    /// <summary>
    /// 模板尺寸和版式的格子尺寸是否一致（误差 <see cref="RenderLimits.LayoutSizeToleranceMm"/> 以内）。
    /// </summary>
    /// <param name="layout">版式。</param>
    /// <param name="templateWidth">模板宽度，mm。</param>
    /// <param name="templateHeight">模板高度，mm。</param>
    /// <returns>一致时为 true。</returns>
    public static bool Fits(SheetLayout layout, double templateWidth, double templateHeight) =>
        Math.Abs(layout.Slot.Width - templateWidth) <= RenderLimits.LayoutSizeToleranceMm
        && Math.Abs(layout.Slot.Height - templateHeight) <= RenderLimits.LayoutSizeToleranceMm;

    /// <summary>
    /// 长度按 0.1 mm 取整，名称、编码去掉首尾空白；返回新对象，不改传入的版式。缺少的部分保持为 null，留给 <see cref="Validate"/> 报告。
    /// </summary>
    /// <param name="layout">用户提交的版式。</param>
    /// <returns>取整后的版式。</returns>
    public static SheetLayout Normalize(SheetLayout layout) => new()
    {
        Code = (layout.Code ?? "").Trim(),
        Name = (layout.Name ?? "").Trim(),
        Paper = layout.Paper is null ? null! : new SheetSize { Width = Round(layout.Paper.Width), Height = Round(layout.Paper.Height) },
        Slot = layout.Slot is null ? null! : new SheetSize { Width = Round(layout.Slot.Width), Height = Round(layout.Slot.Height) },
        Slots = layout.Slots?.Select(slot => slot is null ? null! : new SheetSlot { X = Round(slot.X), Y = Round(slot.Y), Rotation = slot.Rotation }).ToList() ?? null!,
        Offset = layout.Offset is null ? new SheetOffset() : new SheetOffset { X = Round(layout.Offset.X), Y = Round(layout.Offset.Y) },
        CutLines = layout.CutLines,
    };

    /// <summary>
    /// 按 0.1 mm 四舍五入；非有限值原样返回，留给检查报告。
    /// </summary>
    /// <param name="value">长度，mm。</param>
    /// <returns>取整后的长度。</returns>
    private static double Round(double value) => double.IsFinite(value) ? Math.Round(value, 1, MidpointRounding.AwayFromZero) : value;

    /// <summary>
    /// 长度是否大于 0 且不超过上限。
    /// </summary>
    /// <param name="value">长度，mm。</param>
    /// <param name="max">上限，mm。</param>
    /// <returns>在范围里时为 true。</returns>
    private static bool InRange(double value, double max) => double.IsFinite(value) && value > 0 && value <= max;

    /// <summary>
    /// 版式编码的正则：字母开头，字母、数字、下划线、连字符，最长 64 个字符（和模板编码同一规则）。
    /// </summary>
    /// <returns>编译好的正则。</returns>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
