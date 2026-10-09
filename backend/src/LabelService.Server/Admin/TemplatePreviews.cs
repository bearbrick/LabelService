using System.Collections.Concurrent;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Validation;
using LabelService.Server.Templates;

namespace LabelService.Server.Admin;

/// <summary>
/// 模板缩略图：用当前内容（草稿或最新发布版本）和样例值渲染一张小 PNG，模板库的卡片用。
/// </summary>
/// <remarks>
/// 宽度约 360 像素（按画布宽度算 DPI），按“模板编码 + 修改时间”缓存，模板改了自动换新的；缓存有上限，超过时清空。
/// 样例值缺失或不合法时用占位值，渲染失败（如条码内容不对）返回 null，前端显示占位图。
/// </remarks>
internal sealed class TemplatePreviews(ILabelRenderer renderer)
{
    /// <summary>
    /// 缓存上限（张）。
    /// </summary>
    public const int Capacity = 512;

    /// <summary>
    /// 缩略图的目标宽度，像素。
    /// </summary>
    public const double TargetWidth = 360;

    private readonly ConcurrentDictionary<(string Code, long Ticks), byte[]> thumbnails = new();

    /// <summary>
    /// 取缩略图，没有时渲染一张。
    /// </summary>
    public byte[]? Thumbnail(string code, DateTime updatedAt, LabelTemplate template)
    {
        var key = (code, updatedAt.Ticks);
        if (thumbnails.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var dpi = (int)Math.Clamp(Math.Round(TargetWidth / Math.Max(1, template.Page.Width) * LabelUnits.MmPerInch), 30, 203);
        byte[] png;
        try
        {
            png = Render(template, dpi).Content.ToArray();
        }
        catch (LabelRenderException)
        {
            return null;
        }

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
    public RenderResult Render(LabelTemplate template, int dpi, bool mono = false)
    {
        var data = TemplatePublishValidator.SampleData(template.Fields, []);
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
}
