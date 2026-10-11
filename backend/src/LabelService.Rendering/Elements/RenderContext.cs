using LabelService.Contracts;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Binding;
using LabelService.Rendering.Fonts;
using SkiaSharp;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 渲染一张标签时各绘制器共用的上下文：分辨率、字段值、字体、素材、单位换算和警告收集。
/// </summary>
/// <param name="dpi">实际分辨率。</param>
/// <param name="mono">是否输出黑白图。</param>
/// <param name="values">校验后的字段值。</param>
/// <param name="fields">模板字段定义，按 key 索引。</param>
/// <param name="fonts">字体注册表。</param>
/// <param name="assets">素材库。</param>
/// <param name="warnings">本次渲染的警告。</param>
internal sealed class RenderContext(
    int dpi,
    bool mono,
    IReadOnlyDictionary<string, object?> values,
    IReadOnlyDictionary<string, FieldDefinition> fields,
    FontRegistry fonts,
    AssetLibrary assets,
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
    /// 字体注册表。
    /// </summary>
    public FontRegistry Fonts { get; } = fonts;

    /// <summary>
    /// 素材库：图标和图片素材。
    /// </summary>
    public AssetLibrary Assets { get; } = assets;

    /// <summary>
    /// 毫米换算成打印点。
    /// </summary>
    /// <param name="mm">毫米。</param>
    /// <returns>打印点。</returns>
    public float Dots(double mm) => (float)LabelUnits.MmToDots(mm, Dpi);

    /// <summary>
    /// 元素未旋转时的框（打印点）。
    /// </summary>
    /// <param name="element">元素。</param>
    /// <returns>框的位置和大小。</returns>
    public SKRect Box(LabelElement element) =>
        SKRect.Create(Dots(element.X), Dots(element.Y), Dots(element.Width), Dots(element.Height));

    /// <summary>
    /// 替换文本里的绑定。
    /// </summary>
    /// <param name="text">带 {{key:format}} 的文本。</param>
    /// <returns>替换后的文本；引用了没有值的字段时给警告并替换成空。</returns>
    public string Bind(string text) => BindingResolver.Resolve(text, Values, Fields, Warnings);

    /// <summary>
    /// 解析 #RRGGBB 颜色；为空或非法时返回 <paramref name="fallback"/>。
    /// </summary>
    /// <param name="value">颜色文本。</param>
    /// <param name="fallback">默认颜色。</param>
    /// <returns>颜色。</returns>
    public static SKColor Color(string? value, SKColor fallback) =>
        !string.IsNullOrEmpty(value) && SKColor.TryParse(value, out var color) ? color : fallback;
}

/// <summary>
/// 去重的警告列表，保留首次出现的顺序。
/// </summary>
internal sealed class WarningList : ICollection<string>
{
    /// <summary>
    /// 警告，按首次出现的顺序。
    /// </summary>
    private readonly List<string> items = [];

    /// <summary>
    /// 已有的警告，用来去重。
    /// </summary>
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public int Count => items.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <summary>
    /// 加一条警告；已经有同样的就不加。
    /// </summary>
    /// <param name="item">警告，如 TEXT_CLIPPED:t1。</param>
    public void Add(string item)
    {
        if (seen.Add(item))
        {
            items.Add(item);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        items.Clear();
        seen.Clear();
    }

    /// <inheritdoc />
    public bool Contains(string item) => seen.Contains(item);

    /// <inheritdoc />
    public void CopyTo(string[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(string item) => seen.Remove(item) && items.Remove(item);

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => items.GetEnumerator();

    /// <inheritdoc />
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => items.GetEnumerator();
}
