using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Diagnostics;
using LabelService.Rendering.Validation;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// 渲染引擎：尺寸换算、已实现元素的位置、旋转、隐藏、计时。
/// </summary>
/// <remarks>
/// 像素检查只选“按设计必然空白”或“已实现元素”的位置；实现新元素后在这里补对应检查。
/// </remarks>
internal static class RenderChecks
{
    public static void Run(string root, Action<string, bool> check)
    {
        var template = Fixtures.LoadTemplate(root);
        var values = FieldValidator.Validate(template.Fields, null, Fixtures.Data(Fixtures.BoxLabelData)).Values;
        var renderer = new LabelRenderer();
        RenderResult Render(LabelTemplate source, int dpi = 300) => renderer.Render(new RenderJob
        {
            Template = source,
            Labels = [new LabelInstance(values)],
            Format = LabelFormat.Png,
            Dpi = dpi,
        });
        int Dot(double mm, int dpi = 300) => (int)Math.Round(LabelUnits.MmToDots(mm, dpi));
        bool Dark(SKBitmap bitmap, double xMm, double yMm) => bitmap.GetPixel(Dot(xMm), Dot(yMm)).Red < 128;

        var result = Render(template);
        using var bitmap = SKBitmap.Decode(result.Content.ToArray());
        check("100×60mm、300dpi 输出 1181×709 的 PNG", bitmap is { Width: 1181, Height: 709 } && result.ContentType == "image/png");
        check("渲染结果的张数和 DPI", result is { LabelCount: 1, Dpi: 300 });
        check("矩形外框画在框内侧", Dark(bitmap, 1.2, 30) && !Dark(bitmap, 0.5, 30));
        check("横线位于 y=25mm", Dark(bitmap, 50, 25) && !Dark(bitmap, 50, 24.5));
        check("横线和条码之间是空白", !Dark(bitmap, 50, 27.5));
        check("计时记录了 layout、draw、encode",
            new[] { TimingStages.Layout, TimingStages.Draw, TimingStages.Encode }.All(stage => result.Timings.Stages.Any(item => item.Key == stage)));

        using var small = SKBitmap.Decode(Render(template, 203).Content.ToArray());
        check("203dpi 输出 799×480", small is { Width: 799, Height: 480 });

        var hidden = Fixtures.LoadTemplate(root);
        hidden.Elements.Single(element => element.Id == "frame").Hidden = true;
        using var hiddenBitmap = SKBitmap.Decode(Render(hidden).Content.ToArray());
        check("隐藏的元素不渲染", !Dark(hiddenBitmap, 1.2, 30));

        var rotated = new LabelTemplate
        {
            Code = "ROTATE",
            Page = new LabelPage { Width = 40, Height = 60 },
            Elements = [new LineElement { Id = "l", X = 10, Y = 30, Width = 20, Height = 0, Rotation = 90, Stroke = new StrokeStyle { Width = 0.5 } }],
        };
        using var rotatedBitmap = SKBitmap.Decode(Render(rotated).Content.ToArray());
        check("旋转绕元素中心：横线转 90° 变成竖线", Dark(rotatedBitmap, 20, 24) && Dark(rotatedBitmap, 20, 36) && !Dark(rotatedBitmap, 12, 30));

        check("未实现的格式抛 NotSupportedException",
            renderer.Supports(LabelFormat.Pdf) || Throws<NotSupportedException>(() => renderer.Render(new RenderJob
            {
                Template = template,
                Labels = [new LabelInstance(values)],
                Format = LabelFormat.Pdf,
                Dpi = 300,
            })));

        var timings = new RenderTimings();
        timings.Add("custom", 2);
        timings.Add(TimingStages.Draw, 1.5);
        timings.Add(TimingStages.Auth, 0.3);
        timings.Add(TimingStages.Draw, 1);
        var header = timings.ToServerTimingHeader();
        check("Server-Timing 按规范顺序输出、同名累加、保留 1 位小数", header == "auth;dur=0.3, draw;dur=2.5, custom;dur=2.0");
    }

    private static bool Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T)
        {
            return true;
        }
    }
}
