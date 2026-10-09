using LabelService.Contracts;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Binding;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 渲染一张标签时各绘制器共用的上下文：分辨率、字段值、单位换算和警告收集。
/// </summary>
internal sealed class RenderContext(
    int dpi,
    bool mono,
    IReadOnlyDictionary<string, object?> values,
    IReadOnlyDictionary<string, FieldDefinition> fields,
    WarningList warnings)
{
    /// <summary>
    /// 实际分辨率。
    /// </summary>
    public int Dpi { get; } = dpi;

    /// <summary>
    /// 是否输出黑白图。
    /// </summary>
    public bool Mono { get; } = mono;

    /// <summary>
    /// 校验后的字段值。
    /// </summary>
    public IReadOnlyDictionary<string, object?> Values { get; } = values;

    /// <summary>
    /// 模板字段定义，按 key 索引。
    /// </summary>
    public IReadOnlyDictionary<string, FieldDefinition> Fields { get; } = fields;

    /// <summary>
    /// 本次渲染的警告。
    /// </summary>
    public WarningList Warnings { get; } = warnings;

    /// <summary>
    /// 毫米换算成打印点。
    /// </summary>
    public float Dots(double mm) => (float)LabelUnits.MmToDots(mm, Dpi);

    /// <summary>
    /// 元素未旋转时的框（打印点）。
    /// </summary>
    public SKRect Box(LabelElement element) =>
        SKRect.Create(Dots(element.X), Dots(element.Y), Dots(element.Width), Dots(element.Height));

    /// <summary>
    /// 替换文本里的绑定。
    /// </summary>
    public string Bind(string text) => BindingResolver.Resolve(text, Values, Fields, Warnings);

    /// <summary>
    /// 解析 #RRGGBB 颜色；为空或非法时返回 <paramref name="fallback"/>。
    /// </summary>
    public static SKColor Color(string? value, SKColor fallback) =>
        !string.IsNullOrEmpty(value) && SKColor.TryParse(value, out var color) ? color : fallback;
}

/// <summary>
/// 去重的警告列表，保留首次出现的顺序。
/// </summary>
internal sealed class WarningList : ICollection<string>
{
    private readonly List<string> items = [];
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);

    public int Count => items.Count;

    public bool IsReadOnly => false;

    public void Add(string item)
    {
        if (seen.Add(item))
        {
            items.Add(item);
        }
    }

    public void Clear()
    {
        items.Clear();
        seen.Clear();
    }

    public bool Contains(string item) => seen.Contains(item);

    public void CopyTo(string[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

    public bool Remove(string item) => seen.Remove(item) && items.Remove(item);

    public IEnumerator<string> GetEnumerator() => items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => items.GetEnumerator();
}
