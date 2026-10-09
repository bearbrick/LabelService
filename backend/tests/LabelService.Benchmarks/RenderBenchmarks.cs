using System.Text.Json;
using BenchmarkDotNet.Attributes;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
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

    private readonly LabelRenderer renderer = new();
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
    /// 加载内嵌的示例模板。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        using var stream = typeof(RenderBenchmarks).Assembly.GetManifestResourceStream("templates/box-label.json")
            ?? throw new InvalidOperationException("缺少内嵌模板 templates/box-label.json");
        template = JsonSerializer.Deserialize<LabelTemplate>(stream, LabelJson.Options)!;
        data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Data)!;
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
}
