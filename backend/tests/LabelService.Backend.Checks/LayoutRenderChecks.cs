using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using LabelService.Contracts;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Domain.Layouts;
using LabelService.Infrastructure.Storage;
using LabelService.Rendering;
using LabelService.Rendering.Sheets;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// 拼版渲染（T-018）：格子位置（逐格 ≤ 0.1 mm）、旋转方向、起始格子和分页、打印偏移、裁切线、固定元素只排版一次、输出格式。
/// </summary>
internal static partial class LayoutRenderChecks
{
    private const int Dpi = 300;

    public static void Run(string backendRoot, Action<string, bool> check)
    {
        var renderer = Fixtures.Renderer(backendRoot);
        var layout = TestLayout();
        check("检查用的版式（含 90°、180°、270° 的格子）本身合法", SheetLayoutRules.Validate(layout).Count == 0);

        CheckPositions(renderer, layout, check);
        CheckPaging(backendRoot, renderer, check);
        CheckOffsetAndCutLines(renderer, check);
        CheckStaticElements(backendRoot, renderer, check);
        CheckValidator(check);
    }

    private static void CheckPositions(LabelRenderer renderer, SheetLayout layout, Action<string, bool> check)
    {
        var result = renderer.Render(Job(FrameTemplate(), layout, LabelFormat.Png, Labels(layout.Slots.Count)));
        using var page = SKBitmap.Decode(result.Content.ToArray());
        check("拼版 PNG 一页，尺寸是纸张的打印点（A4 300 dpi 2480×3508），PageCount 1、LabelCount 5",
            page.Width == 2480 && page.Height == 3508 && result.PageCount == 1 && result.LabelCount == 5 && !result.IsZip);

        var worst = 0.0;
        for (var i = 0; i < layout.Slots.Count; i++)
        {
            worst = Math.Max(worst, EdgeError(page, layout, layout.Slots[i]));
        }

        Console.WriteLine($"INFO: 拼版格子边框和版式坐标的最大偏差 {worst:0.000} mm");
        check("逐格验证：标签外框的四条边和版式坐标的偏差都不超过 0.1 mm（含旋转的格子）", worst <= 0.1);

        // 标记块在标签左上角 (3,3)–(11,7) mm，中心 (7,5)。顺时针转 90° 后应在格子的右上角，180° 在右下角，270° 在左下角。
        var marks = layout.Slots.Select(slot => (Dark(page, layout, slot, 7, 5), Dark(page, layout, slot, 93, 55))).ToArray();
        check("旋转方向正确：0°、90°、180°、270° 的格子里，标记块都在标签自己的左上角，对角位置是空白",
            marks.All(mark => mark.Item1 && !mark.Item2));
    }

    private static void CheckPaging(string backendRoot, LabelRenderer renderer, Action<string, bool> check)
    {
        var sample = SeedFiles.ReadLayouts(Path.Combine(backendRoot, "samples", "layouts")).Single(item => item.Code == "A4_100x60_2x4");
        var box = Fixtures.LoadTemplate(backendRoot);
        var values = BoxValues(backendRoot, box);
        check("示例版式 A4_100x60_2x4 合法，格子尺寸和示例模板（100×60）一致",
            SheetLayoutRules.Validate(sample).Count == 0 && SheetLayoutRules.Fits(sample, box.Page.Width, box.Page.Height));

        var single = renderer.Render(Job(box, sample, LabelFormat.Pdf, [new LabelInstance(values, 8)]));
        check("单张 8 份 + 8 格版式：PDF 1 页 8 张", single.PageCount == 1 && single.LabelCount == 8 && PdfPages(single) == 1);

        var batch = renderer.Render(Job(box, sample, LabelFormat.Pdf, Enumerable.Range(0, 19).Select(_ => new LabelInstance(values)).ToList(), startSlot: 3));
        var size = MediaBox().Match(Encoding.Latin1.GetString(batch.Content.Span));
        check("19 张 + startSlot 3：3 页（6 + 8 + 5），PDF 页面是纸张大小（A4 595×842 pt）",
            batch.PageCount == 3 && batch.LabelCount == 19 && PdfPages(batch) == 3
            && Math.Abs(double.Parse(size.Groups["w"].Value, System.Globalization.CultureInfo.InvariantCulture) - 595.28) < 0.5
            && Math.Abs(double.Parse(size.Groups["h"].Value, System.Globalization.CultureInfo.InvariantCulture) - 841.89) < 0.5);

        var layout = TestLayout();
        var png = renderer.Render(Job(FrameTemplate(), layout, LabelFormat.Png, Labels(4), startSlot: 3));
        using var zip = new ZipArchive(new MemoryStream(png.Content.ToArray()));
        check("PNG 多页打包 ZIP：每页一张图，文件名 模板编码_页号.png",
            png.IsZip && png.ContentType == "application/zip" && png.Extension == "zip" && png.PageCount == 2
            && zip.Entries.Select(entry => entry.FullName).SequenceEqual(["FRAME_001.png", "FRAME_002.png"]));
        using var first = Decode(zip.Entries[0]);
        using var second = Decode(zip.Entries[1]);
        check("startSlot 3：第一页前两个格子空着，从第 3 格开始放；第二页从第 1 格接着放",
            !Dark(first, layout, layout.Slots[0], 7, 5) && !Dark(first, layout, layout.Slots[1], 7, 5)
            && Dark(first, layout, layout.Slots[2], 7, 5) && Dark(first, layout, layout.Slots[4], 7, 5)
            && Dark(second, layout, layout.Slots[0], 7, 5) && !Dark(second, layout, layout.Slots[1], 7, 5));
    }

    private static void CheckOffsetAndCutLines(LabelRenderer renderer, Action<string, bool> check)
    {
        var shifted = TestLayout(offsetX: 2, offsetY: -1.5);
        var result = renderer.Render(Job(FrameTemplate(), shifted, LabelFormat.Png, Labels(1)));
        using var page = SKBitmap.Decode(result.Content.ToArray());
        check("打印偏移作用于整页：偏移 (2, −1.5) mm 后外框位置仍和“坐标 + 偏移”一致", EdgeError(page, shifted, shifted.Slots[0]) <= 0.1);

        // 没有边框的模板：格子边上的像素只有裁切线会画。
        var plain = FrameTemplate(frame: false);
        bool EdgeInked(bool cutLines)
        {
            var layout = TestLayout(cutLines: cutLines);
            using var bitmap = SKBitmap.Decode(renderer.Render(Job(plain, layout, LabelFormat.Png, Labels(2))).Content.ToArray());
            bool TopEdge(SheetSlot slot)
            {
                var origin = SheetRenderer.SlotOrigin(layout, slot, Dpi);
                var x = origin.X + (int)Dots(layout.Footprint(slot).Width / 2);
                return Enumerable.Range(origin.Y - 1, 3).Any(y => IsDark(bitmap, x, y));
            }

            // 放了 2 张：第 2 格有标签，第 4 格空着。
            return TopEdge(layout.Slots[1]) && !TopEdge(layout.Slots[3]);
        }

        check("裁切线可以开关：cutLines 为 true 时放了标签的格子画边框，空格子和 cutLines 为 false 时不画", EdgeInked(true) && !EdgeInked(false));
    }

    private static void CheckStaticElements(string backendRoot, LabelRenderer renderer, Action<string, bool> check)
    {
        var box = Fixtures.LoadTemplate(backendRoot);
        var fixedCount = box.Elements.Count(LabelPlan.IsStatic);
        check("示例箱标里既有固定元素（公司名、标题、线、框、图标）也有数据元素", fixedCount >= 3 && fixedCount < box.Elements.Count);

        var values = BoxValues(backendRoot, box);
        var labels = Enumerable.Range(0, 3).Select(_ => new LabelInstance(values)).ToList();
        var layout = SeedFiles.ReadLayouts(Path.Combine(backendRoot, "samples", "layouts")).Single();
        var shared = renderer.Render(Job(box, layout, LabelFormat.Png, labels));
        var separate = renderer.Render(Job(box, layout, LabelFormat.Png, labels, share: false));
        check("固定元素只排版一次、各格子回放，和逐格重新排版画出来的像素完全一样", shared.Content.Span.SequenceEqual(separate.Content.Span));
    }

    private static void CheckValidator(Action<string, bool> check)
    {
        var overlap = TestLayout();
        overlap.Slots[1].X = 90;
        var outside = TestLayout();
        outside.Slots[4].Y = 200;
        var rotation = TestLayout();
        rotation.Slots[0].Rotation = 45;
        var touching = TestLayout();
        touching.Slots[1].X = 105.04;
        check("版式检查：格子重叠、超出纸张、旋转不是 90 的倍数都报出来并带格子序号；贴边（0.05 mm 以内）不算重叠",
            SheetLayoutRules.Validate(overlap).Any(item => item.Contains("第 2 个格子和第 1 个格子重叠"))
            && SheetLayoutRules.Validate(outside).Any(item => item.Contains("第 5 个格子超出了纸张"))
            && SheetLayoutRules.Validate(rotation).Any(item => item.Contains("第 1 个格子的旋转"))
            && SheetLayoutRules.Validate(touching).Count == 0
            && SheetLayoutRules.Validate(new SheetLayout { Code = "1A", Name = "x", Slots = [new SheetSlot()] }).Any(item => item.Contains("编码")));
    }

    /// <summary>
    /// 格子所占矩形四条边的实测位置和期望位置（坐标 + 偏移）的最大偏差，mm。从格子外往里找第一个深色像素。
    /// </summary>
    private static double EdgeError(SKBitmap page, SheetLayout layout, SheetSlot slot)
    {
        var (width, height) = layout.Footprint(slot);
        var left = Dots(slot.X + layout.Offset.X);
        var top = Dots(slot.Y + layout.Offset.Y);
        var right = left + Dots(width);
        var bottom = top + Dots(height);
        var midX = (int)((left + right) / 2);
        var midY = (int)((top + bottom) / 2);
        const int search = 15;
        double Scan(int from, int step, Func<int, bool> dark)
        {
            for (var i = 0; i <= 2 * search; i++)
            {
                if (dark(from + i * step))
                {
                    return from + i * step;
                }
            }

            return double.NaN;
        }

        var errors = new[]
        {
            Scan((int)left - search, 1, x => IsDark(page, x, midY)) - left,
            Scan((int)Math.Ceiling(right) + search, -1, x => IsDark(page, x, midY)) + 1 - right,
            Scan((int)top - search, 1, y => IsDark(page, midX, y)) - top,
            Scan((int)Math.Ceiling(bottom) + search, -1, y => IsDark(page, midX, y)) + 1 - bottom,
        };
        return errors.Max(error => double.IsNaN(error) ? double.MaxValue : Math.Abs(error)) * LabelUnits.MmPerInch / Dpi;
    }

    /// <summary>
    /// 标签坐标 (u, v) mm 按格子的旋转换算到纸上，看那里是不是深色（3×3 平均）。
    /// </summary>
    private static bool Dark(SKBitmap page, SheetLayout layout, SheetSlot slot, double u, double v)
    {
        var (w, h) = (layout.Slot.Width, layout.Slot.Height);
        var (x, y) = slot.Rotation switch
        {
            90 => (h - v, u),
            180 => (w - u, h - v),
            270 => (v, w - u),
            _ => (u, v),
        };
        var px = (int)Dots(slot.X + layout.Offset.X + x);
        var py = (int)Dots(slot.Y + layout.Offset.Y + y);
        var sum = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                sum += page.GetPixel(Math.Clamp(px + dx, 0, page.Width - 1), Math.Clamp(py + dy, 0, page.Height - 1)).Red;
            }
        }

        return sum / 9 < 160;
    }

    private static bool IsDark(SKBitmap page, int x, int y) => x >= 0 && y >= 0 && x < page.Width && y < page.Height && page.GetPixel(x, y).Red < 128;

    private static double Dots(double mm) => LabelUnits.MmToDots(mm, Dpi);

    private static SKBitmap Decode(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return SKBitmap.Decode(memory.ToArray());
    }

    private static int PdfPages(RenderResult result) => PdfPage().Count(Encoding.Latin1.GetString(result.Content.Span));

    private static RenderJob Job(LabelTemplate template, SheetLayout layout, LabelFormat format, IReadOnlyList<LabelInstance> labels, int startSlot = 1, bool share = true) => new()
    {
        Template = template,
        Labels = labels,
        Format = format,
        Dpi = Dpi,
        Layout = layout,
        StartSlot = startSlot,
        ShareStaticElements = share,
    };

    private static List<LabelInstance> Labels(int count) =>
        Enumerable.Range(1, count).Select(i => new LabelInstance(new Dictionary<string, object?> { ["no"] = "第 " + i + " 张" })).ToList();

    private static IReadOnlyDictionary<string, object?> BoxValues(string backendRoot, LabelTemplate box)
    {
        var request = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(backendRoot, "samples", "requests", "box-label.render.json")));
        var data = request.RootElement.GetProperty("data").EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone());
        return Rendering.Validation.FieldValidator.Validate(box.Fields, null, data).Values;
    }

    /// <summary>
    /// 检查用的 A4 版式：两个正放的格子，再加 90°、180°、270° 各一个。
    /// </summary>
    private static SheetLayout TestLayout(double offsetX = 0, double offsetY = 0, bool cutLines = false) => new()
    {
        Code = "CHECK_A4",
        Name = "检查用",
        Paper = new SheetSize { Width = 210, Height = 297 },
        Slot = new SheetSize { Width = 100, Height = 60 },
        Slots =
        [
            new SheetSlot { X = 5, Y = 10 },
            new SheetSlot { X = 107, Y = 10 },
            new SheetSlot { X = 5, Y = 80, Rotation = 90 },
            new SheetSlot { X = 70, Y = 80, Rotation = 180 },
            new SheetSlot { X = 70, Y = 150, Rotation = 270 },
        ],
        Offset = new SheetOffset { X = offsetX, Y = offsetY },
        CutLines = cutLines,
    };

    /// <summary>
    /// 100×60 的检查模板：0.5 mm 外框（可去掉）、左上角一个实心标记块、中间一行随数据变化的文字。
    /// </summary>
    private static LabelTemplate FrameTemplate(bool frame = true)
    {
        List<LabelElement> elements =
        [
            new RectElement { Id = "mark", X = 3, Y = 3, Width = 8, Height = 4, Fill = "#000000" },
            new TextElement { Id = "no", X = 20, Y = 25, Width = 60, Height = 10, Content = "{{no}}" },
        ];
        if (frame)
        {
            elements.Insert(0, new RectElement { Id = "frame", Width = 100, Height = 60, Stroke = new StrokeStyle { Width = 0.5 } });
        }

        return new LabelTemplate
        {
            Code = "FRAME",
            Name = "检查用外框",
            Page = new LabelPage { Width = 100, Height = 60, Dpi = Dpi },
            Fields = [new FieldDefinition { Key = "no", Required = false }],
            Elements = elements,
        };
    }

    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PdfPage();

    [GeneratedRegex(@"/MediaBox\s*\[\s*0\s+0\s+(?<w>[\d.]+)\s+(?<h>[\d.]+)\s*\]")]
    private static partial Regex MediaBox();
}
