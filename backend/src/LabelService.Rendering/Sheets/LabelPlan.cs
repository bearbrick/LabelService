using LabelService.Contracts.Templates;
using LabelService.Rendering.Elements;
using SkiaSharp;

namespace LabelService.Rendering.Sheets;

/// <summary>
/// 拼版时一个模板的绘制计划：元素按叠放顺序分成“固定”和“随数据变化”的连续段。
/// 固定段（不引用字段的文字、Logo、边框、线）排版一次、录成 SKPicture，各格子直接回放；随数据变化的段每张标签排版一次。
/// </summary>
/// <remarks>
/// 分段保持叠放顺序不变：固定元素压在数据元素上面时，回放顺序也一样。
/// </remarks>
internal sealed class LabelPlan : IDisposable
{
    private readonly List<Segment> segments = [];

    private LabelPlan()
    {
    }

    /// <summary>
    /// 随数据变化的段数，<see cref="LayoutDynamic"/> 返回同样多的排版结果。
    /// </summary>
    public int DynamicSegments => segments.Count(segment => segment.Elements is not null);

    /// <summary>
    /// 分段，并把固定段排版、录制好。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="context">没有字段值的上下文（固定元素用不到字段值）。</param>
    /// <param name="width">标签宽度（打印点）。</param>
    /// <param name="height">标签高度（打印点）。</param>
    /// <param name="shareStatic">false 时全部当作随数据变化（基准对比用）。</param>
    public static LabelPlan Create(LabelTemplate template, RenderContext context, int width, int height, bool shareStatic)
    {
        var plan = new LabelPlan();
        List<(IElementPainter Painter, ElementLayout Layout)>? fixedRun = null;
        List<(LabelElement Element, IElementPainter Painter)>? dynamicRun = null;
        try
        {
            foreach (var element in template.Elements)
            {
                if (LabelRenderer.PainterFor(element) is not { } painter)
                {
                    continue;
                }

                if (shareStatic && IsStatic(element))
                {
                    if (dynamicRun is not null)
                    {
                        plan.segments.Add(new Segment(null, dynamicRun));
                        dynamicRun = null;
                    }

                    (fixedRun ??= []).Add((painter, painter.Layout(element, context)));
                    continue;
                }

                if (fixedRun is not null)
                {
                    plan.segments.Add(new Segment(Record(fixedRun, context, width, height), null));
                    fixedRun = null;
                }

                (dynamicRun ??= []).Add((element, painter));
            }

            if (fixedRun is not null)
            {
                plan.segments.Add(new Segment(Record(fixedRun, context, width, height), null));
            }

            if (dynamicRun is not null)
            {
                plan.segments.Add(new Segment(null, dynamicRun));
            }

            return plan;
        }
        catch
        {
            plan.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 用一张标签的字段值排版随数据变化的段。
    /// </summary>
    public List<(IElementPainter Painter, ElementLayout Layout)>[] LayoutDynamic(RenderContext context)
    {
        var result = new List<(IElementPainter, ElementLayout)>[DynamicSegments];
        var index = 0;
        foreach (var segment in segments)
        {
            if (segment.Elements is { } elements)
            {
                result[index++] = elements.Select(item => (item.Painter, item.Painter.Layout(item.Element, context))).ToList();
            }
        }

        return result;
    }

    /// <summary>
    /// 按叠放顺序画一张标签：固定段回放录好的画面，数据段画 <paramref name="dynamic"/>。坐标是标签自己的打印点。
    /// </summary>
    public void Draw(SKCanvas canvas, List<(IElementPainter Painter, ElementLayout Layout)>[] dynamic, RenderContext context)
    {
        var index = 0;
        foreach (var segment in segments)
        {
            if (segment.Picture is { } picture)
            {
                canvas.DrawPicture(picture);
                continue;
            }

            foreach (var (painter, layout) in dynamic[index++])
            {
                LabelRenderer.Draw(canvas, painter, layout, context);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var segment in segments)
        {
            segment.Picture?.Dispose();
        }

        segments.Clear();
    }

    /// <summary>
    /// 元素内容不引用字段时是固定的：文字、条码、二维码、图片看有没有 <c>{{</c>，图标、线、矩形总是固定的。
    /// </summary>
    internal static bool IsStatic(LabelElement element) => element switch
    {
        TextElement text => !HasBinding(text.Content),
        BarcodeElement barcode => !HasBinding(barcode.Value),
        QrCodeElement qrCode => !HasBinding(qrCode.Value),
        ImageElement image => !HasBinding(image.Src),
        IconElement or LineElement or RectElement => true,
        _ => false,
    };

    private static bool HasBinding(string value) => value.Contains("{{", StringComparison.Ordinal);

    private static SKPicture Record(List<(IElementPainter Painter, ElementLayout Layout)> run, RenderContext context, int width, int height)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(width, height));
        foreach (var (painter, layout) in run)
        {
            LabelRenderer.Draw(canvas, painter, layout, context);
        }

        return recorder.EndRecording();
    }

    /// <summary>
    /// 一段：固定段只有 <see cref="Picture"/>，数据段只有 <see cref="Elements"/>。
    /// </summary>
    private sealed record Segment(SKPicture? Picture, List<(LabelElement Element, IElementPainter Painter)>? Elements);
}
