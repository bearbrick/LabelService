using System.Diagnostics;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Elements;
using LabelService.Rendering.Fonts;
using LabelService.Rendering.Output;
using LabelService.Rendering.Sheets;
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
    /// <summary>
    /// 字体注册表。
    /// </summary>
    private readonly FontRegistry fonts;

    /// <summary>
    /// 素材库。
    /// </summary>
    private readonly AssetLibrary assets;

    /// <summary>
    /// 不带字体和素材的渲染引擎：文本用 Skia 默认字体并给 FONT_FALLBACK 警告，图标、图片素材都找不到。只给不涉及文本和素材的检查用。
    /// </summary>
    public LabelRenderer()
        : this(FontRegistry.Empty)
    {
    }

    /// <summary>
    /// 使用指定字体、不带素材的渲染引擎。
    /// </summary>
    /// <param name="fonts">字体注册表。</param>
    public LabelRenderer(FontRegistry fonts)
        : this(fonts, AssetLibrary.Empty)
    {
    }

    /// <summary>
    /// 使用指定字体和素材的渲染引擎。字体和素材都在启动时加载，渲染期间只读。
    /// </summary>
    /// <param name="fonts">字体注册表。</param>
    /// <param name="assets">素材库。</param>
    public LabelRenderer(FontRegistry fonts, AssetLibrary assets)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(assets);
        this.fonts = fonts;
        this.assets = assets;
    }

    /// <inheritdoc />
    public bool Supports(LabelFormat format) => OutputDocuments.IsSupported(format);

    /// <inheritdoc />
    public RenderResult Render(RenderJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Layout is not null)
        {
            return SheetRenderer.Render(job, fonts, assets);
        }

        // 拼版另走 SheetRenderer；下面是一张标签一页（或一个 ^XA）的普通渲染。
        var template = job.Template;
        var timings = job.Timings;
        var warnings = new WarningList();
        var fields = template.Fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        var width = (int)Math.Round(LabelUnits.MmToDots(template.Page.Width, job.Dpi));
        var height = (int)Math.Round(LabelUnits.MmToDots(template.Page.Height, job.Dpi));

        // ZPL 只能打黑白：绘制器按黑白画（文字线条不抗锯齿，图片抖动），输出时再按阈值转 1 位。
        var mono = job.Mono || job.Format == LabelFormat.Zpl;
        using var document = OutputDocuments.Create(job.Format, width, height, job.Dpi, job.Mono, template.Page);
        var labelCount = 0;
        for (var index = 0; index < job.Labels.Count; index++)
        {
            // 每张标签：排版（layout 阶段）→ 绘制（draw 阶段）→ 结束这张（encode 阶段），各自计时。
            var label = job.Labels[index];
            var start = Stopwatch.GetTimestamp();
            var context = new RenderContext(job.Dpi, mono, label.Values, fields, fonts, assets, warnings);
            List<(IElementPainter Painter, ElementLayout Layout)> layouts;
            try
            {
                layouts = Layout(template, context);
            }
            catch (LabelRenderException ex) when (job.Labels.Count > 1 && ex.LabelIndex is null)
            {
                // 批量合并时告诉调用方是哪一条的数据有问题。
                throw ex.ForLabel(index);
            }

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

    /// <summary>
    /// 排版一张标签的全部元素（按元素顺序，后面的画在上面）。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="context">这张标签的渲染上下文。</param>
    /// <returns>每个要画的元素的绘制器和排版结果。</returns>
    /// <exception cref="LabelRenderException">某个元素的数据无法渲染。</exception>
    private static List<(IElementPainter Painter, ElementLayout Layout)> Layout(LabelTemplate template, RenderContext context)
    {
        var layouts = new List<(IElementPainter, ElementLayout)>(template.Elements.Count);
        foreach (var element in template.Elements)
        {
            if (PainterFor(element) is { } painter)
            {
                layouts.Add((painter, painter.Layout(element, context)));
            }
        }

        return layouts;
    }

    /// <summary>
    /// 元素的绘制器；隐藏的元素和没有绘制器的元素类型返回 null（跳过）。
    /// </summary>
    /// <param name="element">元素。</param>
    /// <returns>绘制器，或 null。</returns>
    internal static IElementPainter? PainterFor(LabelElement element) => element.Hidden ? null : ElementPainters.Find(element.Type);

    /// <summary>
    /// 画一个已排版的元素，统一处理旋转。单张和拼版共用。
    /// </summary>
    /// <param name="canvas">画布（单位是打印点）。</param>
    /// <param name="painter">绘制器。</param>
    /// <param name="layout">排版结果。</param>
    /// <param name="context">渲染上下文。</param>
    internal static void Draw(SKCanvas canvas, IElementPainter painter, ElementLayout layout, RenderContext context)
    {
        var rotation = (float)layout.Rotation;
        if (rotation == 0)
        {
            painter.Draw(canvas, layout, context);
            return;
        }

        canvas.Save();
        canvas.RotateDegrees(rotation, layout.Pivot.X, layout.Pivot.Y);
        painter.Draw(canvas, layout, context);
        canvas.Restore();
    }
}
