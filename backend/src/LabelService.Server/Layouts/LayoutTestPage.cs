using System.Globalization;
using System.Text.Json;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Templates;
using LabelService.Rendering;

namespace LabelService.Server.Layouts;

/// <summary>
/// 打印校准测试页（设计书第 4 节“打印校准”）：每个格子画边框、中心十字和格子序号，按版式（含打印偏移）排一页。
/// 打出来量格子边框和纸上实际位置的差，填进版式的打印偏移。
/// </summary>
/// <remarks>
/// 用一个和格子同尺寸的临时模板走正常的拼版渲染，每个格子一张标签、序号是字段值，所以和真实打印的位置完全一致。
/// </remarks>
internal static class LayoutTestPage
{
    /// <summary>
    /// 测试页的渲染任务：一页、每个格子一张。
    /// </summary>
    public static RenderJob Job(SheetLayout layout, int dpi, Contracts.Protocol.LabelFormat format)
    {
        var template = Template(layout);
        var labels = Enumerable.Range(1, layout.Slots.Count)
            .Select(number => new LabelInstance(new Dictionary<string, object?> { ["n"] = number.ToString(CultureInfo.InvariantCulture) }))
            .ToList();
        return new RenderJob
        {
            Template = template,
            Labels = labels,
            Format = format,
            Dpi = dpi,
            Layout = layout,
        };
    }

    private static LabelTemplate Template(SheetLayout layout)
    {
        var width = layout.Slot.Width;
        var height = layout.Slot.Height;
        var cross = Math.Min(10, Math.Min(width, height) / 2);
        var line = new StrokeStyle { Width = 0.2 };
        return new LabelTemplate
        {
            Code = "LAYOUT_TEST",
            Name = "打印校准测试页",
            Page = new LabelPage { Width = width, Height = height },
            Fields = [new FieldDefinition { Key = "n", Name = "格子序号", Required = true, Sample = JsonSerializer.SerializeToElement("1") }],
            Elements =
            [
                new RectElement { Id = "frame", Width = width, Height = height, Stroke = line },
                new LineElement { Id = "crossH", X = (width - cross) / 2, Y = height / 2, Width = cross, Stroke = line },
                new LineElement { Id = "crossV", X = width / 2, Y = (height - cross) / 2, Height = cross, Stroke = line },
                new TextElement
                {
                    Id = "number",
                    X = 1.5,
                    Y = 1,
                    Width = Math.Max(1, Math.Min(20, width - 3)),
                    Height = Math.Max(1, Math.Min(6, height - 2)),
                    Content = "{{n}}",
                    Style = new TextStyle { FontSize = 8 },
                },
            ],
        };
    }
}
