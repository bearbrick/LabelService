using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using LabelService.Contracts;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Diagnostics;
using LabelService.Rendering.Elements;
using LabelService.Rendering.Fonts;
using LabelService.Rendering.Output;
using SkiaSharp;

namespace LabelService.Rendering.Sheets;

/// <summary>
/// 拼版渲染（ADR 0005，设计书第 7 节）：按版式的格子顺序依次放标签，一页排满换下一页。PDF 一页一张纸；PNG 一页一张图，多页时打包 ZIP。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>每个格子把画布平移、旋转到格子位置，裁到标签大小，再按单张的方式画。格子位置对齐到整数个打印点，条码模块不会因为平移落在半个点上。</item>
/// <item>不引用字段的元素一次请求只排版、绘制一次（<see cref="LabelPlan"/>），各格子回放。</item>
/// <item>整页加上版式的打印偏移；cutLines 为 true 时给放了标签的格子画裁切线。</item>
/// </list>
/// </remarks>
internal static class SheetRenderer
{
    private static readonly IReadOnlyDictionary<string, object?> NoValues = new Dictionary<string, object?>();

    /// <summary>
    /// 渲染拼版任务。版式、起始格子、格子尺寸和模板尺寸一致，都由接口层校验过。
    /// </summary>
    public static RenderResult Render(RenderJob job, FontRegistry fonts, AssetLibrary assets)
    {
        var layout = job.Layout ?? throw new ArgumentException("没有版式。", nameof(job));
        if (job.Format is not (LabelFormat.Pdf or LabelFormat.Png))
        {
            throw new InvalidOperationException($"拼版只支持 PDF 和 PNG，{job.Format} 应在接口层拒绝。");
        }

        if (job.StartSlot < 1 || job.StartSlot > layout.Slots.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(job), $"起始格子 {job.StartSlot} 不在 1–{layout.Slots.Count}，应在接口层拒绝。");
        }

        var template = job.Template;
        var timings = job.Timings;
        var dpi = job.Dpi;
        var warnings = new WarningList();
        var fields = template.Fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        var label = new SKSizeI(Dots(template.Page.Width, dpi), Dots(template.Page.Height, dpi));
        var total = job.Labels.Sum(item => item.Copies);
        var pageCount = (job.StartSlot - 1 + total + layout.Slots.Count - 1) / layout.Slots.Count;

        var start = Stopwatch.GetTimestamp();
        using var plan = CreatePlan(job, new RenderContext(dpi, job.Mono, NoValues, fields, fonts, assets, warnings), label);
        start = timings.Mark(TimingStages.Layout, start);

        using var document = new SheetDocument(job, layout, template.Code);
        using var cutLine = CutLinePaint(dpi);
        var position = job.StartSlot - 1;
        var filled = new List<int>(layout.Slots.Count);
        SKCanvas? canvas = null;
        for (var index = 0; index < job.Labels.Count; index++)
        {
            var item = job.Labels[index];
            var context = new RenderContext(dpi, job.Mono, item.Values, fields, fonts, assets, warnings);
            List<(IElementPainter Painter, ElementLayout Layout)>[] dynamic;
            try
            {
                dynamic = plan.LayoutDynamic(context);
            }
            catch (LabelRenderException ex) when (job.Labels.Count > 1 && ex.LabelIndex is null)
            {
                throw ex.ForLabel(index);
            }

            start = timings.Mark(TimingStages.Layout, start);
            for (var copy = 0; copy < item.Copies; copy++, position++)
            {
                var slot = position % layout.Slots.Count;
                if (canvas is null || (slot == 0 && filled.Count > 0))
                {
                    if (canvas is not null)
                    {
                        start = FinishPage(document, canvas, layout, filled, label, dpi, cutLine, timings, start);
                    }

                    canvas = document.BeginPage();
                }

                DrawSlot(canvas, layout, layout.Slots[slot], label, dpi, plan, dynamic, context);
                filled.Add(slot);
            }

            start = timings.Mark(TimingStages.Draw, start);
        }

        if (canvas is not null)
        {
            FinishPage(document, canvas, layout, filled, label, dpi, cutLine, timings, start);
        }

        var encodeStart = Stopwatch.GetTimestamp();
        var (content, isZip) = document.Finish();
        timings.Mark(TimingStages.Encode, encodeStart);
        return new RenderResult
        {
            Content = content,
            Format = job.Format,
            LabelCount = total,
            PageCount = pageCount,
            IsZip = isZip,
            Dpi = dpi,
            Warnings = warnings.ToList(),
            Timings = timings,
        };
    }

    /// <summary>
    /// 格子在纸上的左上角（打印点，含打印偏移，取整）。
    /// </summary>
    internal static SKPointI SlotOrigin(SheetLayout layout, SheetSlot slot, int dpi) =>
        new(Dots(slot.X + layout.Offset.X, dpi), Dots(slot.Y + layout.Offset.Y, dpi));

    private static LabelPlan CreatePlan(RenderJob job, RenderContext context, SKSizeI label)
    {
        try
        {
            return LabelPlan.Create(job.Template, context, label.Width, label.Height, job.ShareStaticElements);
        }
        catch (LabelRenderException ex) when (job.Labels.Count > 1 && ex.LabelIndex is null)
        {
            // 固定元素出错和单张时一样算在第一条上。
            throw ex.ForLabel(0);
        }
    }

    private static void DrawSlot(
        SKCanvas canvas,
        SheetLayout layout,
        SheetSlot slot,
        SKSizeI label,
        int dpi,
        LabelPlan plan,
        List<(IElementPainter Painter, ElementLayout Layout)>[] dynamic,
        RenderContext context)
    {
        var origin = SlotOrigin(layout, slot, dpi);
        canvas.Save();
        canvas.Translate(origin.X, origin.Y);

        // 顺时针旋转后标签仍要落在格子所占的矩形里：先平移到旋转后左上角对应的位置再旋转。
        switch (slot.Rotation)
        {
            case 90:
                canvas.Translate(label.Height, 0);
                canvas.RotateDegrees(90);
                break;
            case 180:
                canvas.Translate(label.Width, label.Height);
                canvas.RotateDegrees(180);
                break;
            case 270:
                canvas.Translate(0, label.Width);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.ClipRect(SKRect.Create(label.Width, label.Height));
        plan.Draw(canvas, dynamic, context);
        canvas.Restore();
    }

    private static long FinishPage(SheetDocument document, SKCanvas canvas, SheetLayout layout, List<int> filled, SKSizeI label, int dpi, SKPaint cutLine, RenderTimings timings, long start)
    {
        if (layout.CutLines)
        {
            foreach (var index in filled)
            {
                var slot = layout.Slots[index];
                var origin = SlotOrigin(layout, slot, dpi);
                var (width, height) = slot.Rotation is 90 or 270 ? (label.Height, label.Width) : (label.Width, label.Height);
                canvas.DrawRect(SKRect.Create(origin.X, origin.Y, width, height), cutLine);
            }
        }

        filled.Clear();
        start = timings.Mark(TimingStages.Draw, start);
        document.EndPage();
        return timings.Mark(TimingStages.Encode, start);
    }

    private static SKPaint CutLinePaint(int dpi) => new()
    {
        Color = new SKColor(0x66, 0x66, 0x66),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = (float)LabelUnits.MmToDots(0.1, dpi),
        IsAntialias = false,
    };

    private static int Dots(double mm, int dpi) => (int)Math.Round(LabelUnits.MmToDots(mm, dpi));

    /// <summary>
    /// 输出：PDF 一个文档、每页一张纸；PNG 每页一个文档，最后一页以上打包 ZIP。
    /// </summary>
    private sealed class SheetDocument(RenderJob job, SheetLayout layout, string templateCode) : IDisposable
    {
        private readonly int width = Dots(layout.Paper.Width, job.Dpi);
        private readonly int height = Dots(layout.Paper.Height, job.Dpi);
        private readonly List<byte[]> pngPages = [];
        private OutputDocument? pdf;
        private OutputDocument? png;

        public SKCanvas BeginPage()
        {
            if (job.Format == LabelFormat.Pdf)
            {
                pdf ??= OutputDocuments.Create(LabelFormat.Pdf, width, height, job.Dpi, job.Mono, new LabelPage { Width = layout.Paper.Width, Height = layout.Paper.Height });
                return pdf.BeginLabel();
            }

            png = OutputDocuments.Create(LabelFormat.Png, width, height, job.Dpi, job.Mono, new LabelPage { Width = layout.Paper.Width, Height = layout.Paper.Height });
            return png.BeginLabel();
        }

        public void EndPage()
        {
            if (pdf is not null)
            {
                pdf.EndLabel(1);
                return;
            }

            png!.EndLabel(1);
            using var buffer = new MemoryStream();
            png.WriteTo(buffer);
            pngPages.Add(buffer.ToArray());
            png.Dispose();
            png = null;
        }

        public (ReadOnlyMemory<byte> Content, bool IsZip) Finish()
        {
            var output = new MemoryStream();
            if (pdf is not null)
            {
                pdf.WriteTo(output);
                return (output.GetBuffer().AsMemory(0, (int)output.Length), false);
            }

            if (pngPages.Count == 1)
            {
                return (pngPages[0], false);
            }

            var digits = Math.Max(3, pngPages.Count.ToString(CultureInfo.InvariantCulture).Length);
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (var i = 0; i < pngPages.Count; i++)
                {
                    // PNG 已经压缩过，ZIP 里不再压缩。
                    var entry = archive.CreateEntry($"{templateCode}_{(i + 1).ToString("D" + digits, CultureInfo.InvariantCulture)}.png", CompressionLevel.NoCompression);
                    using var stream = entry.Open();
                    stream.Write(pngPages[i]);
                }
            }

            return (output.GetBuffer().AsMemory(0, (int)output.Length), true);
        }

        public void Dispose()
        {
            pdf?.Dispose();
            png?.Dispose();
        }
    }
}
