using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Validation;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// 图片和图标（T-004）：SVG 清洗、图标着色、素材的三种 fit、缩放缓存、字段图片、mono 抖动、错误码。
/// </summary>
internal static class AssetChecks
{
    private const int Dpi = 300;

    public static void Run(string backendRoot, Action<string, bool> check)
    {
        CheckSanitizer(check);
        var assets = Fixtures.Assets(backendRoot);
        check("加载了 GB/T 191 图标和内置图片素材，没有加载错误",
            assets.Errors.Count == 0 && assets.IconNames.Contains("gb191/fragile") && assets.ImageIds.Contains("logo"));
        var renderer = Fixtures.Renderer(backendRoot);
        CheckIcons(backendRoot, renderer, check);
        CheckImages(renderer, assets, check);
        CheckFieldImages(renderer, check);
    }

    private static void CheckSanitizer(Action<string, bool> check)
    {
        const string Evil = """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 10 10" onload="alert(1)">
              <script>alert(2)</script>
              <style>@import url(http://evil.example/x.css);</style>
              <foreignObject><div>x</div></foreignObject>
              <image href="http://127.0.0.1:9/a.png" width="10" height="10"/>
              <image xlink:href="file:///etc/passwd" width="10" height="10"/>
              <use href="#dot" style="fill:url(http://evil.example/p)"/>
              <circle id="dot" cx="5" cy="5" r="4" fill="url(#g)" onclick="x()"/>
              <linearGradient id="g"><stop offset="0" stop-color="#000"/></linearGradient>
            </svg>
            """;
        var clean = SvgSanitizer.Sanitize(Evil);
        check("SVG 清洗：去掉脚本、样式表、foreignObject 和 on* 事件属性",
            !clean.Contains("script") && !clean.Contains("@import") && !clean.Contains("foreignObject") && !clean.Contains("onload") && !clean.Contains("onclick"));
        check("SVG 清洗：去掉外部链接（http、file），保留文档内引用 #id 和 url(#g)",
            !clean.Contains("127.0.0.1") && !clean.Contains("file:") && !clean.Contains("evil.example") && clean.Contains("href=\"#dot\"") && clean.Contains("url(#g)"));
        var started = Stopwatch.GetTimestamp();
        using (AssetLibrary.LoadSvg(Evil))
        {
        }

        check("清洗后的 SVG 加载时不访问外部地址（不会卡在网络请求上）", Stopwatch.GetElapsedTime(started).TotalMilliseconds < 1000);
        bool Rejected(string svg)
        {
            try
            {
                SvgSanitizer.Sanitize(svg);
                return false;
            }
            catch (Exception ex) when (ex is XmlException or InvalidDataException)
            {
                return true;
            }
        }

        check("SVG 清洗：拒绝 DTD 实体（防 XXE）和不是 svg 的文档",
            Rejected("""<!DOCTYPE svg [<!ENTITY x SYSTEM "file:///etc/passwd">]><svg xmlns="http://www.w3.org/2000/svg">&x;</svg>""") && Rejected("<html/>"));
    }

    private static void CheckIcons(string backendRoot, LabelRenderer renderer, Action<string, bool> check)
    {
        var template = Fixtures.LoadTemplate(backendRoot);
        var values = FieldValidator.Validate(template.Fields, null, Fixtures.Data(Fixtures.BoxLabelData)).Values;
        using (var sample = Bitmap(renderer.Render(Job(template, values))))
        {
            check("示例箱标的图标画出来了", Count(sample, 76, 52, 6, 6, Dark) > 50);
        }

        using (var red = Bitmap(renderer.Render(Job(Single(new IconElement { Id = "i", X = 5, Y = 5, Width = 20, Height = 20, Icon = "gb191/up", Color = "#C00000" })))))
        {
            check("图标按 color 着色", Count(red, 5, 5, 20, 20, Red) > 200 && Count(red, 5, 5, 20, 20, Dark) == 0);
        }

        check("图标不存在返回 IMAGE_INVALID", Error(renderer, new IconElement { Id = "i", X = 5, Y = 5, Width = 10, Height = 10, Icon = "gb191/nope" }) == ErrorCodes.ImageInvalid);
    }

    private static void CheckImages(LabelRenderer renderer, AssetLibrary assets, Action<string, bool> check)
    {
        // 内置 logo 是 240×80（3:1），放进 30×30 的框。
        ImageElement Logo(ImageFit fit) => new()
        {
            Id = "img",
            X = 5,
            Y = 5,
            Width = 30,
            Height = 30,
            Src = "asset:logo",
            Fit = fit
        };
        using (var contain = Bitmap(renderer.Render(Job(Single(Logo(ImageFit.Contain))))))
        {
            check("fit=contain：等比缩放后居中，上下留白", Count(contain, 5, 15, 30, 10, Dark) > 500 && Count(contain, 5, 5, 30, 9.5, Dark) == 0 && Count(contain, 5, 25.5, 30, 9.5, Dark) == 0);
        }

        renderer.Render(Job(Single(Logo(ImageFit.Stretch))));
        var before = assets.ScaledCount;
        renderer.Render(Job(Single(Logo(ImageFit.Stretch))));
        renderer.Render(Job(Single(Logo(ImageFit.Stretch))));
        check("同一素材同一尺寸只缩放一次（缓存）", assets.ScaledCount == before);
        check("素材不存在返回 IMAGE_INVALID", Error(renderer, new ImageElement { Id = "img", X = 5, Y = 5, Width = 10, Height = 10, Src = "asset:nope" }) == ErrorCodes.ImageInvalid);

        var mono = Logo(ImageFit.Stretch);
        mono.Src = "asset:qcpass";
        mono.Mono = true;
        using (var bitmap = Bitmap(renderer.Render(Job(Single(mono)))))
        {
            check("mono=true：只有纯黑和纯白，没有灰色", Count(bitmap, 5, 5, 30, 30, pixel => pixel.Red is > 0 and < 255) == 0 && Count(bitmap, 5, 5, 30, 30, Dark) > 100);
        }
    }

    private static void CheckFieldImages(LabelRenderer renderer, Action<string, bool> check)
    {
        var template = new LabelTemplate
        {
            Code = "PHOTO",
            Page = new LabelPage { Width = 40, Height = 40 },
            Fields = [new FieldDefinition { Key = "photo", Type = FieldType.Image, Required = false }],
            Elements = [new ImageElement { Id = "p", X = 5, Y = 5, Width = 30, Height = 30, Src = "{{photo}}", Fit = ImageFit.Stretch }],
        };
        IReadOnlyDictionary<string, object?> Values(string? base64) =>
            FieldValidator.Validate(template.Fields, null, base64 is null ? [] : new Dictionary<string, JsonElement> { ["photo"] = JsonSerializer.SerializeToElement(base64) }).Values;

        // 测试图 100×50：从左到右红、绿、蓝、黄四条竖条。框 30×30。
        var stripes = Stripes();
        IReadOnlyDictionary<string, object?> Stripes2(ImageFit fit)
        {
            template.Elements[0] = new ImageElement { Id = "p", X = 5, Y = 5, Width = 30, Height = 30, Src = "{{photo}}", Fit = fit };
            return Values(stripes);
        }

        using (var bitmap = Bitmap(renderer.Render(Job(template, Stripes2(ImageFit.Contain)))))
        {
            check("字段图片 fit=contain：四条都在，等比缩放后居中，上下留白",
                Count(bitmap, 5.5, 18, 6, 4, Is(SKColors.Red)) > 1000 && Count(bitmap, 29.5, 18, 5, 4, Is(SKColors.Yellow)) > 1000 && Count(bitmap, 5, 5, 30, 7, NotWhite) == 0);
        }

        using (var bitmap = Bitmap(renderer.Render(Job(template, Stripes2(ImageFit.Cover)))))
        {
            check("字段图片 fit=cover：等比放大占满框，左右裁掉（只剩中间的绿、蓝两条）",
                Count(bitmap, 6, 6, 6, 28, Is(SKColors.Lime)) > 10000 && Count(bitmap, 28, 6, 6, 28, Is(SKColors.Blue)) > 10000 && Count(bitmap, 5, 5, 30, 30, Is(SKColors.Red)) == 0);
        }

        using (var bitmap = Bitmap(renderer.Render(Job(template, Stripes2(ImageFit.Stretch)))))
        {
            check("字段图片 fit=stretch：拉伸填满框（四条都在，占满高度）",
                Count(bitmap, 6, 6, 5, 28, Is(SKColors.Red)) > 10000 && Count(bitmap, 29, 6, 5, 28, Is(SKColors.Yellow)) > 10000);
        }

        template.Elements[0] = new ImageElement { Id = "p", X = 5, Y = 5, Width = 30, Height = 30, Src = "{{photo}}", Fit = ImageFit.Stretch };
        using (var bitmap = Bitmap(renderer.Render(Job(template, Values(null)))))
        {
            check("可选的图片字段没传时什么都不画，不报错", Count(bitmap, 0, 0, 40, 40, Dark) == 0);
        }

        template.Elements[0] = new ImageElement { Id = "p", X = 5, Y = 5, Width = 30, Height = 30, Src = "{{photo}}", Fit = ImageFit.Stretch, Mono = true };
        using (var bitmap = Bitmap(renderer.Render(Job(template, Values(Png(new SKColor(128, 128, 128)))))))
        {
            var black = Count(bitmap, 5, 5, 30, 30, Dark);
            var total = (int)Math.Pow(LabelUnits.MmToDots(30, Dpi), 2);
            check("mono 抖动：50% 灰抖成大约一半黑点、没有灰色", black > total * 0.4 && black < total * 0.6 && Count(bitmap, 5, 5, 30, 30, pixel => pixel.Red is > 0 and < 255) == 0);
        }
    }

    private static string Stripes()
    {
        using var bitmap = new SKBitmap(100, 50);
        SKColor[] colors = [SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow];
        for (var x = 0; x < 100; x++)
        {
            for (var y = 0; y < 50; y++)
            {
                bitmap.SetPixel(x, y, colors[x / 25]);
            }
        }

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }

    private static Func<SKColor, bool> Is(SKColor color) =>
        pixel => Math.Abs(pixel.Red - color.Red) < 40 && Math.Abs(pixel.Green - color.Green) < 40 && Math.Abs(pixel.Blue - color.Blue) < 40;

    private static bool NotWhite(SKColor pixel) => pixel.Red < 240 || pixel.Green < 240 || pixel.Blue < 240;

    private static string Png(SKColor color)
    {
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }

    private static string? Error(LabelRenderer renderer, LabelElement element)
    {
        try
        {
            renderer.Render(Job(Single(element)));
            return null;
        }
        catch (LabelRenderException ex)
        {
            return ex.ErrorCode;
        }
    }

    private static RenderJob Job(LabelTemplate template, IReadOnlyDictionary<string, object?>? values = null) =>
        new()
        {
            Template = template,
            Labels = [new LabelInstance(values ?? new Dictionary<string, object?>())],
            Format = LabelFormat.Png,
            Dpi = Dpi
        };

    private static LabelTemplate Single(LabelElement element) =>
        new()
        {
            Code = "ASSET",
            Page = new LabelPage { Width = 40, Height = 40 },
            Elements = [element]
        };

    private static SKBitmap Bitmap(RenderResult result) => SKBitmap.Decode(result.Content.ToArray());

    private static bool Dark(SKColor pixel) => pixel.Red < 128 && pixel.Green < 128 && pixel.Blue < 128;

    private static bool Red(SKColor pixel) => pixel.Red > 150 && pixel.Green < 80 && pixel.Blue < 80;

    private static int Count(SKBitmap bitmap, double xMm, double yMm, double widthMm, double heightMm, Func<SKColor, bool> match)
    {
        int Dot(double mm) => (int)Math.Round(LabelUnits.MmToDots(mm, Dpi));
        var count = 0;
        for (var y = Math.Max(0, Dot(yMm)); y < Math.Min(bitmap.Height, Dot(yMm + heightMm)); y++)
        {
            for (var x = Math.Max(0, Dot(xMm)); x < Math.Min(bitmap.Width, Dot(xMm + widthMm)); x++)
            {
                if (match(bitmap.GetPixel(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
