using System.Globalization;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Fonts;
using LabelService.Rendering.Validation;

namespace LabelService.Benchmarks;

/// <summary>
/// 拼版：A4 一页 24 张 70×37 mm 的标签（3 列 × 8 行，每张 12 个元素、箱号各不相同），300 dpi。
/// 预算：PDF 每页 P95 ≤ 250 ms（ADR 0005）。<see cref="ShareStatic"/> 对比“固定元素只排版一次”的效果。
/// PDF、PNG 都测（门禁要覆盖两种格式）；PNG 的时间主要花在 A4 整页的 PNG 编码上。
/// </summary>
[MemoryDiagnoser]
public class SheetBenchmarks
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
    /// 70×37 的小箱标模板。
    /// </summary>
    private LabelTemplate template = null!;

    /// <summary>
    /// A4 3 列 × 8 行的版式。
    /// </summary>
    private SheetLayout layout = null!;

    /// <summary>
    /// 校验好的 24 张标签数据（箱号各不相同）。
    /// </summary>
    private List<LabelInstance> labels = null!;

    /// <summary>
    /// 输出格式。
    /// </summary>
    [Params(LabelFormat.Pdf, LabelFormat.Png)]
    public LabelFormat Format
    {
        get; set;
    }

    /// <summary>
    /// 固定元素是否只排版一次（默认 true；false 时每个格子重新排版）。
    /// </summary>
    [Params(true, false)]
    public bool ShareStatic
    {
        get; set;
    }

    /// <summary>
    /// 准备模板、版式和 24 条数据（校验不计入）。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        fonts = FontRegistry.Load(BenchmarkPaths.FontDirectory());
        var root = Path.GetDirectoryName(BenchmarkPaths.FontDirectory())!;
        assets = AssetLibrary.Load(Path.Combine(root, "icons"), [Path.Combine(root, "images")]);
        renderer = new LabelRenderer(fonts, assets);
        template = Template();
        layout = new SheetLayout
        {
            Code = "A4_70x37_3x8",
            Name = "A4 · 70×37 · 3 列 × 8 行",
            Paper = new SheetSize { Width = 210, Height = 297 },
            Slot = new SheetSize { Width = 70, Height = 37 },
            Slots = Enumerable.Range(0, 24).Select(i => new SheetSlot { X = i % 3 * 70, Y = 0.5 + (i / 3 * 37) }).ToList(),
        };
        labels = Enumerable.Range(1, 24).Select(i => new LabelInstance(FieldValidator.Validate(template.Fields, null, new Dictionary<string, JsonElement>
        {
            ["partName"] = JsonSerializer.SerializeToElement("电源适配器"),
            ["partNo"] = JsonSerializer.SerializeToElement("3100-0123"),
            ["qty"] = JsonSerializer.SerializeToElement(50),
            ["boxNo"] = JsonSerializer.SerializeToElement("C26100800" + i.ToString("D2", CultureInfo.InvariantCulture)),
        }).Values)).ToList();
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
    /// 渲染一页 24 张。
    /// </summary>
    /// <returns>输出文件的字节数（返回结果防止被优化掉）。</returns>
    [Benchmark]
    public int A4Sheet24()
    {
        var result = renderer.Render(new RenderJob
        {
            Template = template,
            Labels = labels,
            Format = Format,
            Dpi = 300,
            Layout = layout,
            ShareStaticElements = ShareStatic,
        });
        return result.Content.Length;
    }

    /// <summary>
    /// 70×37 的小箱标：外框、公司名、标题、横线、图标（固定）；品名、料号、数量、箱号文字、一维码、二维码（随数据变化）。
    /// </summary>
    /// <returns>模板。</returns>
    private static LabelTemplate Template()
    {
        TextElement Text(string id, double x, double y, double width, string content, double size = 7, bool bold = false) =>
            new()
            {
                Id = id,
                X = x,
                Y = y,
                Width = width,
                Height = 4,
                Content = content,
                Style = new TextStyle { FontSize = size, Bold = bold }
            };
        return new LabelTemplate
        {
            Code = "SMALL_BOX",
            Name = "小箱标",
            Page = new LabelPage { Width = 70, Height = 37, Dpi = 300 },
            Fields =
            [
                new FieldDefinition { Key = "partName" },
                new FieldDefinition { Key = "partNo" },
                new FieldDefinition { Key = "qty", Type = FieldType.Number },
                new FieldDefinition { Key = "boxNo" },
            ],
            Elements =
            [
                new RectElement { Id = "frame", X = 1, Y = 1, Width = 68, Height = 35, Stroke = new StrokeStyle { Width = 0.3 } },
                Text("company", 3, 2.5, 40, "XX 科技有限公司"),
                Text("title", 48, 2.5, 19, "成品箱标", 8, true),
                new LineElement { Id = "rule", X = 3, Y = 7.5, Width = 64, Stroke = new StrokeStyle { Width = 0.2 } },
                new IconElement { Id = "fragile", Icon = "gb191/fragile", X = 62, Y = 29, Width = 5, Height = 5 },
                Text("partName", 3, 9, 40, "品名：{{partName}}"),
                Text("partNo", 3, 13.5, 40, "料号：{{partNo}}"),
                Text("qty", 44, 9, 23, "数量：{{qty}}"),
                Text("boxNo", 3, 18, 40, "箱号：{{boxNo}}"),
                new BarcodeElement { Id = "barcode", X = 3, Y = 23, Width = 40, Height = 11, Value = "{{boxNo}}" },
                new QrCodeElement { Id = "qr", X = 47, Y = 14, Width = 14, Height = 14, Value = "{{partNo}}|{{boxNo}}" },
                Text("note", 44, 13.5, 23, "合格", 7),
            ],
        };
    }
}
