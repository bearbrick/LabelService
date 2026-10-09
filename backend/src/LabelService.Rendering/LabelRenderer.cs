using System.Diagnostics;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Elements;
using LabelService.Rendering.Output;
using SkiaSharp;

namespace LabelService.Rendering;

/// <summary>
/// 默认渲染引擎：排版 → 绘制 → 输出编码，三种格式共用同一套排版和绘图代码。
/// </summary>
/// <remarks>
/// 绘图坐标一律是打印点（dot），原点在标签左上角。元素旋转在这里统一处理，各元素的绘制器只画未旋转的框。
/// 热路径不访问数据库、磁盘或网络；字体、图标等资源在启动时加载（见 docs/performance.md）。
/// </remarks>
public sealed class LabelRenderer : ILabelRenderer
{
    /// <inheritdoc />
    public bool Supports(LabelFormat format) => OutputDocuments.IsSupported(format);

    /// <inheritdoc />
    public RenderResult Render(RenderJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var template = job.Template;
        var timings = job.Timings;
        var warnings = new WarningList();
        var fields = template.Fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        var width = (int)Math.Round(LabelUnits.MmToDots(template.Page.Width, job.Dpi));
        var height = (int)Math.Round(LabelUnits.MmToDots(template.Page.Height, job.Dpi));

        using var document = OutputDocuments.Create(job.Format, width, height, job.Dpi, job.Mono);
        var labelCount = 0;
        foreach (var label in job.Labels)
        {
            var start = Stopwatch.GetTimestamp();
            var context = new RenderContext(job.Dpi, job.Mono, label.Values, fields, warnings);
            var layouts = Layout(template, context);
            start = timings.Mark(TimingStages.Layout, start);

            var canvas = document.BeginLabel();
            foreach (var (painter, layout) in layouts)
            {
                Draw(canvas, painter, layout, context);
            }

            start = timings.Mark(TimingStages.Draw, start);
            document.EndLabel(label.Copies);
            timings.Mark(TimingStages.Encode, start);
            labelCount += label.Copies;
        }

        var encodeStart = Stopwatch.GetTimestamp();
        var output = new MemoryStream();
        document.WriteTo(output);
        timings.Mark(TimingStages.Encode, encodeStart);

        return new RenderResult
        {
            Content = output.GetBuffer().AsMemory(0, (int)output.Length),
            Format = job.Format,
            LabelCount = labelCount,
            Dpi = job.Dpi,
            Warnings = warnings.ToList(),
            Timings = timings,
        };
    }

    private static List<(IElementPainter Painter, ElementLayout Layout)> Layout(LabelTemplate template, RenderContext context)
    {
        var layouts = new List<(IElementPainter, ElementLayout)>(template.Elements.Count);
        foreach (var element in template.Elements)
        {
            if (element.Hidden)
            {
                continue;
            }

            // 尚未实现的元素类型先跳过，实现进度见 docs/tasks/README.md。
            var painter = ElementPainters.Find(element.Type);
            if (painter is null)
            {
                continue;
            }

            layouts.Add((painter, painter.Layout(element, context)));
        }

        return layouts;
    }

    private static void Draw(SKCanvas canvas, IElementPainter painter, ElementLayout layout, RenderContext context)
    {
        var rotation = (float)layout.Element.Rotation;
        if (rotation == 0)
        {
            painter.Draw(canvas, layout, context);
            return;
        }

        canvas.Save();
        canvas.RotateDegrees(rotation, layout.Bounds.MidX, layout.Bounds.MidY);
        painter.Draw(canvas, layout, context);
        canvas.Restore();
    }
}
