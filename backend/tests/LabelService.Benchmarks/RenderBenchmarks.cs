using System.Text.Json;
using BenchmarkDotNet.Attributes;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Fonts;
using LabelService.Rendering.Validation;

namespace LabelService.Benchmarks;

/// <summary>
/// 单张渲染流水线：校验 + 排版 + 绘制 + 编码，不含 HTTP。对照 docs/performance.md 的单张预算。
/// </summary>
[MemoryDiagnoser]
public class RenderBenchmarks
{
    private const string Data =
        """{"partName":"电源适配器","partNo":"3100-0123","qty":50,"batchNo":"20261008-A","prodDate":"2026-10-08","boxNo":"C2610080001"}""";

    private FontRegistry fonts = null!;
    private AssetLibrary assets = null!;
    private LabelRenderer renderer = null!;
    private LabelTemplate template = null!;
    private Dictionary<string, JsonElement> data = null!;

    /// <summary>
    /// 分辨率。203 是热敏标签机最常见的规格，300 是默认值。
    /// </summary>
    [Params(203, 300)]
    public int Dpi
    {
        get; set;
    }

    /// <summary>
    /// 加载内嵌的示例模板，以及 backend/assets 里的字体、图标和图片素材。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        using var stream = typeof(RenderBenchmarks).Assembly.GetManifestResourceStream("templates/box-label.json")
            ?? throw new InvalidOperationException("缺少内嵌模板 templates/box-label.json");
        template = JsonSerializer.Deserialize<LabelTemplate>(stream, LabelJson.Options)!;
        data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Data)!;
        fonts = FontRegistry.Load(FontDirectory());
        if (!fonts.HasDefaultFamily)
        {
            throw new InvalidOperationException("backend/assets/fonts 里没有思源黑体，见那里的 README");
        }

        var root = Path.GetDirectoryName(FontDirectory())!;
        assets = AssetLibrary.Load(Path.Combine(root, "icons"), [Path.Combine(root, "images")]);
        renderer = new LabelRenderer(fonts, assets);
    }

    /// <summary>
    /// 释放字体和素材。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        fonts.Dispose();
        assets.Dispose();
    }

    /// <summary>
    /// 成品箱标 100×60mm 输出 PNG。
    /// </summary>
    [Benchmark]
    public int BoxLabelPng()
    {
        var values = FieldValidator.Validate(template.Fields, null, data).Values;
        var result = renderer.Render(new RenderJob
        {
            Template = template,
            Labels = [new LabelInstance(values)],
            Format = LabelFormat.Png,
            Dpi = Dpi,
        });
        return result.Content.Length;
    }

    /// <summary>
    /// 成品箱标 100×60mm 输出 PDF（矢量，字体嵌入子集）。
    /// </summary>
    [Benchmark]
    public int BoxLabelPdf() => Render(LabelFormat.Pdf);

    private int Render(LabelFormat format)
    {
        var values = FieldValidator.Validate(template.Fields, null, data).Values;
        var result = renderer.Render(new RenderJob
        {
            Template = template,
            Labels = [new LabelInstance(values)],
            Format = format,
            Dpi = Dpi,
        });
        return result.Content.Length;
    }

    private static string FontDirectory()
    {
        // BenchmarkDotNet 在 bin 下的子目录里运行，往上找到 backend 目录。
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "LabelService.Backend.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("未找到 LabelService.Backend.slnx"), "assets", "fonts");
    }
}
