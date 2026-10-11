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
/// 每个示例模板 × PNG / PDF / ZPL 的单张渲染（排版 + 绘制 + 编码；校验在准备阶段做一次，不计入；不含 HTTP），按模板自己的 DPI，用字段的样例值。
/// </summary>
/// <remarks>
/// 新增示例模板（backend/samples/templates）后在 <see cref="Template"/> 的参数里加上它，再更新基线（见 docs/performance.md）。
/// </remarks>
[MemoryDiagnoser]
public class TemplateBenchmarks
{
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
    /// 当前参数对应的示例模板。
    /// </summary>
    private LabelTemplate template = null!;
    /// <summary>
    /// 用样例值校验出的字段值。
    /// </summary>
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
    /// 加载模板、字体、素材，用样例值校验一次（校验不计入每次调用，单张流水线含校验的耗时见 RenderBenchmarks）。
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
    /// <returns>输出文件的字节数（返回结果防止被优化掉）。</returns>
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

    /// <summary>
    /// 模板字段的样例值，当作接口收到的数据（没有样例的字段不传）。
    /// </summary>
    /// <returns>字段 key → 样例值。</returns>
    private Dictionary<string, JsonElement> SampleData() =>
        template.Fields.Where(field => field.Sample is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) })
            .ToDictionary(field => field.Key, field => field.Sample!.Value);
}
