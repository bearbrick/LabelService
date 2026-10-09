using System.Text.RegularExpressions;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Assets;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 图片：<c>src</c> 是 <c>asset:素材ID</c> 时取素材库，是 <c>{{字段}}</c> 时取 image 字段的 PNG / JPG。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>fit：contain 等比缩放后居中，cover 等比放大后居中裁掉多余部分，stretch 拉伸填满。和设计器一致。</item>
/// <item>mono 为 true（或整张输出黑白）时用 Floyd–Steinberg 抖动成黑白。</item>
/// <item>素材按目标尺寸栅格化后缓存；字段里的图片每次请求都不一样，每次解码、缩放。</item>
/// <item>素材不存在、字段里的图片解码失败抛 IMAGE_INVALID；可选字段没传时什么都不画。</item>
/// </list>
/// </remarks>
internal sealed partial class ImagePainter : IElementPainter
{
    public string ElementType => ElementTypes.Image;

    public ElementLayout Layout(LabelElement element, RenderContext context)
    {
        var image = (ImageElement)element;
        var box = context.Box(element);
        var src = image.Src.Trim();
        var mono = image.Mono || context.Mono;
        if (src.StartsWith("asset:", StringComparison.Ordinal))
        {
            var id = src["asset:".Length..];
            if (!context.Assets.TryGetImage(id, out var asset))
            {
                throw new LabelRenderException(ErrorCodes.ImageInvalid, $"元素 {element.Id} 引用的图片素材 {id} 不存在", element.Id);
            }

            var target = Pixels(ImageRaster.Destination(asset.Size, box.Width, box.Height, image.Fit), box);
            return new ImageLayout(element, box, context.Assets.Scaled(id, asset, target.Width, target.Height, image.Fit, mono), target, owned: false);
        }

        var match = FieldReference().Match(src);
        if (!match.Success)
        {
            return new ImageLayout(element, box, null, default, owned: false);
        }

        var key = match.Groups["key"].Value;
        if (!context.Fields.ContainsKey(key))
        {
            context.Warnings.Add(WarningCodes.UnknownBinding + ":" + Uri.EscapeDataString(key));
            return new ImageLayout(element, box, null, default, owned: false);
        }

        if (!context.Values.TryGetValue(key, out var value) || value is not byte[] bytes || bytes.Length == 0)
        {
            return new ImageLayout(element, box, null, default, owned: false);
        }

        using var decoded = SKImage.FromEncodedData(bytes)
            ?? throw new LabelRenderException(ErrorCodes.ImageInvalid, $"字段 {key} 的图片无法解码", element.Id);
        var source = new ImageSource(decoded, null);
        var area = Pixels(ImageRaster.Destination(source.Size, box.Width, box.Height, image.Fit), box);
        return new ImageLayout(element, box, ImageRaster.Render(source, area.Width, area.Height, image.Fit, mono), area, owned: true);
    }

    public void Draw(SKCanvas canvas, ElementLayout layout, RenderContext context)
    {
        var image = (ImageLayout)layout;
        if (image.Image is null)
        {
            return;
        }

        try
        {
            canvas.Save();
            canvas.ClipRect(layout.Bounds);
            // 栅格化的尺寸和区域一致，1:1 画上去；旋转时由画布采样。
            canvas.DrawImage(image.Image, image.Area.Left, image.Area.Top, new SKSamplingOptions(SKFilterMode.Linear));
            canvas.Restore();
        }
        finally
        {
            if (image.Owned)
            {
                image.Image.Dispose();
            }
        }
    }

    /// <summary>
    /// 图片在画布上占的整像素区域：左上角取整，栅格化的尺寸和它一致，画的时候 1:1，不再缩放。
    /// </summary>
    private static SKRectI Pixels(SKRect destination, SKRect box)
    {
        var left = (int)Math.Round(box.Left + destination.Left);
        var top = (int)Math.Round(box.Top + destination.Top);
        return SKRectI.Create(left, top, Math.Max(1, (int)Math.Round(destination.Width)), Math.Max(1, (int)Math.Round(destination.Height)));
    }

    [GeneratedRegex(@"^\{\{\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\}\}$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldReference();

    /// <summary>
    /// 图片的排版结果。字段图片每次请求单独生成，画完释放；素材图片来自缓存，不释放。
    /// </summary>
    private sealed class ImageLayout(LabelElement element, SKRect bounds, SKImage? image, SKRectI area, bool owned)
        : ElementLayout(element, bounds)
    {
        public SKImage? Image { get; } = image;

        public SKRectI Area { get; } = area;

        public bool Owned { get; } = owned;
    }
}
