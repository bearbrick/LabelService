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
    /// <summary>
    /// 成品箱标的完整数据（和检查程序的示例数据一致）。
    /// </summary>
    private const string Data =
        """{"partName":"电源适配器","partNo":"3100-0123","qty":50,"batchNo":"20261008-A","prodDate":"2026-10-08","boxNo":"C2610080001"}""";

    /// <summary>
    /// backend/assets/fonts 里的字体。
    /// </summary>
    private FontRegistry fonts = null!;
    /// <summary>
    /// backend/assets 里的图标和图片素材。
    /// </summary>
    private AssetLibrary assets = null!;
    /// <summary>
    /// 渲染引擎（和服务端一样带字体和素材）。
    /// </summary>
    private LabelRenderer renderer = null!;
    /// <summary>
    /// 成品箱标模板。
    /// </summary>
    private LabelTemplate template = null!;
    /// <summary>
    /// 接口收到的字段数据（每次调用都要重新校验）。
    /// </summary>
    private Dictionary<string, JsonElement> data = null!;
    /// <summary>
    /// 箱号流水，<see cref="BoxLabelPngUniqueBox"/> 每次加一。
    /// </summary>
    private int boxSerial;

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
        fonts = FontRegistry.Load(BenchmarkPaths.FontDirectory());
        if (!fonts.HasDefaultFamily)
        {
            throw new InvalidOperationException("backend/assets/fonts 里没有思源黑体，见那里的 README");
        }

        var root = Path.GetDirectoryName(BenchmarkPaths.FontDirectory())!;
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
    /// <returns>输出文件的字节数（返回结果防止被优化掉）。</returns>
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
    /// 成品箱标输出 PNG，每次箱号都不同：一维码、二维码的编码缓存不命中，更接近真实调用（每箱一个箱号）。
    /// </summary>
    /// <returns>输出文件的字节数。</returns>
    [Benchmark]
    public int BoxLabelPngUniqueBox()
    {
        var unique = new Dictionary<string, JsonElement>(data)
        {
            ["boxNo"] = JsonSerializer.SerializeToElement($"C26100{Interlocked.Increment(ref boxSerial):D5}"),
        };
        var values = FieldValidator.Validate(template.Fields, null, unique).Values;
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
    /// <returns>输出文件的字节数。</returns>
    [Benchmark]
    public int BoxLabelPdf() => Render(LabelFormat.Pdf);

    /// <summary>
    /// 成品箱标 100×60mm 输出 ZPL（黑白位图 + ASCII 压缩）。
    /// </summary>
    /// <returns>输出文件的字节数。</returns>
    [Benchmark]
    public int BoxLabelZpl() => Render(LabelFormat.Zpl);

    /// <summary>
    /// 校验字段并渲染一张成品箱标。
    /// </summary>
    /// <param name="format">输出格式。</param>
    /// <returns>输出文件的字节数。</returns>
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
}
