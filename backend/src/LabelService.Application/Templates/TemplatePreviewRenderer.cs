using System.Collections.Concurrent;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using SkiaSharp;

namespace LabelService.Application.Templates;

/// <summary>
/// 用样例值渲染模板：模板库卡片的缩略图、设计器的服务端预览都用它。
/// </summary>
/// <remarks>
/// 缩略图按模板自己的 DPI 渲染（和发布检查一致：低 DPI 下条码模块可能不足 1 个打印点，渲染会失败），再缩小到约 360 像素宽。
/// 按“模板编码 + 修改时间”缓存，模板改了自动换新的；缓存有上限，超过时清空。
/// 样例值缺失或不合法时用占位值，渲染失败（如条码内容不对）返回 null，前端显示占位图。
/// </remarks>
/// <param name="renderer">渲染引擎。</param>
public sealed class TemplatePreviewRenderer(ILabelRenderer renderer)
{
    /// <summary>
    /// 缩略图缓存的上限（张）。
    /// </summary>
    public const int Capacity = 512;

    /// <summary>
    /// 缩略图的目标宽度，像素。
    /// </summary>
    public const double TargetWidth = 360;

    /// <summary>
    /// 缩略图缓存：（模板编码，修改时间）→ PNG。
    /// </summary>
    private readonly ConcurrentDictionary<(string Code, long Ticks), byte[]> thumbnails = new();

    /// <summary>
    /// 取缩略图，没有时渲染一张。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="updatedAt">模板的修改时间，模板改了缓存自动失效。</param>
    /// <param name="template">模板当前内容（草稿或最新发布版本）。</param>
    /// <returns>PNG；渲染失败时为 null。</returns>
    public byte[]? Thumbnail(string code, DateTime updatedAt, LabelTemplate template)
    {
        var key = (code, updatedAt.Ticks);
        if (thumbnails.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var dpi = template.Page.Dpi > 0 ? template.Page.Dpi : RenderLimits.DefaultDpi;
        byte[] png;
        try
        {
            png = Downscale(Render(template, dpi).Content.ToArray());
        }
        catch (LabelRenderException)
        {
            return null;
        }

        // 超过上限就整个清空：缩略图便宜，重新渲染即可，不值得做淘汰算法。
        if (thumbnails.Count >= Capacity)
        {
            thumbnails.Clear();
        }

        thumbnails[key] = png;
        return png;
    }

    /// <summary>
    /// 用样例值渲染一张 PNG（缩略图、服务端预览共用）。
    /// </summary>
    /// <param name="template">模板内容。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="mono">是否输出黑白（热敏打印效果）。</param>
    /// <returns>渲染结果。</returns>
    /// <exception cref="LabelRenderException">模板或样例值有问题，渲染失败。</exception>
    public RenderResult Render(LabelTemplate template, int dpi, bool mono = false)
    {
        var data = TemplateSampleData.Create(template.Fields, []);
        var values = FieldValidator.Validate(template.Fields, null, data).Values;
        return renderer.Render(new RenderJob
        {
            Template = template,
            Labels = [new LabelInstance(values)],
            Format = LabelFormat.Png,
            Dpi = dpi,
            Mono = mono,
        });
    }

    /// <summary>
    /// 缩小到 <see cref="TargetWidth"/> 像素宽，保持比例；本来就更窄时原样返回。
    /// </summary>
    /// <param name="png">原图。</param>
    /// <returns>缩小后的 PNG。</returns>
    private static byte[] Downscale(byte[] png)
    {
        using var source = SKBitmap.Decode(png);
        if (source is null || source.Width <= TargetWidth)
        {
            return png;
        }

        var info = new SKImageInfo((int)TargetWidth, Math.Max(1, (int)Math.Round(source.Height * TargetWidth / source.Width)));
        using var scaled = source.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
