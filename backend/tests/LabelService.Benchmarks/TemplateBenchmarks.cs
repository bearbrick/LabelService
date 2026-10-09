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
/// 每个示例模板 × PNG / PDF / ZPL 的单张渲染（校验 + 排版 + 绘制 + 编码，不含 HTTP），按模板自己的 DPI，用字段的样例值（T-015）。
/// </summary>
/// <remarks>
/// 新增示例模板（backend/samples/templates）后在 <see cref="Template"/> 的参数里加上它，再更新基线（见 docs/performance.md）。
/// </remarks>
[MemoryDiagnoser]
public class TemplateBenchmarks
{
    private FontRegistry fonts = null!;
    private AssetLibrary assets = null!;
    private LabelRenderer renderer = null!;
    private LabelTemplate template = null!;
    private IReadOnlyDictionary<string, object?> values = null!;

    /// <summary>
    /// 示例模板（backend/samples/templates 里的文件名，不带扩展名）。
    /// </summary>
    [Params("box-label", "a4-shipping-mark")]
    public string Template { get; set; } = "";

    /// <summary>
    /// 输出格式。
    /// </summary>
    [Params(LabelFormat.Png, LabelFormat.Pdf, LabelFormat.Zpl)]
    public LabelFormat Format
    {
        get; set;
    }

    /// <summary>
    /// 加载模板、字体、素材，用样例值校验一次（校验本身也计入每次调用）。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        using var stream = typeof(TemplateBenchmarks).Assembly.GetManifestResourceStream($"templates/{Template}.json")
            ?? throw new InvalidOperationException($"缺少内嵌模板 templates/{Template}.json");
        template = JsonSerializer.Deserialize<LabelTemplate>(stream, LabelJson.Options)!;
        fonts = FontRegistry.Load(BenchmarkPaths.FontDirectory());
        var root = Path.GetDirectoryName(BenchmarkPaths.FontDirectory())!;
        assets = AssetLibrary.Load(Path.Combine(root, "icons"), [Path.Combine(root, "images")]);
        renderer = new LabelRenderer(fonts, assets);
        values = FieldValidator.Validate(template.Fields, null, SampleData()).Values;
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
    /// 渲染一张。
    /// </summary>
    [Benchmark]
    public int Render()
    {
        var result = renderer.Render(new RenderJob
        {
            Template = template,
            Labels = [new LabelInstance(values)],
            Format = Format,
            Dpi = template.Page.Dpi > 0 ? template.Page.Dpi : RenderLimits.DefaultDpi,
        });
        return result.Content.Length;
    }

    private Dictionary<string, JsonElement> SampleData() =>
        template.Fields.Where(field => field.Sample is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) })
            .ToDictionary(field => field.Key, field => field.Sample!.Value);
}
