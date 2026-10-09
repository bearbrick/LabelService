using System.Collections.Concurrent;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 编码好的条码符号：一维码是一行模块，二维码是模块矩阵。只读，多个请求共享。
/// </summary>
/// <param name="Columns">列数（一维码的模块数）。</param>
/// <param name="Rows">行数（一维码为 1）。</param>
/// <param name="Modules">按行存放的模块，true 为黑。</param>
/// <param name="Caption">人眼可读文字，如 EAN-13 补上校验位后的 13 位数字。</param>
internal sealed record EncodedSymbol(int Columns, int Rows, bool[] Modules, string Caption)
{
    /// <summary>
    /// 第 <paramref name="row"/> 行第 <paramref name="column"/> 列是不是黑模块。
    /// </summary>
    public bool this[int column, int row] => Modules[row * Columns + column];
}

/// <summary>
/// 条码编码结果的缓存，按“码制 + 参数 + 内容”索引。线程安全，有容量上限。
/// </summary>
/// <remarks>
/// 同一个模板的固定内容（如二维码里的网址）和批量里重复的内容只编码一次。
/// 超过上限时整个清空：实现简单，最坏情况只是重新编码，不会无限增长。
/// </remarks>
internal static class SymbolCache
{
    /// <summary>
    /// 最多缓存的条目数。
    /// </summary>
    public const int Capacity = 4096;

    private static readonly ConcurrentDictionary<(string Kind, string Content), EncodedSymbol> Entries = new();

    /// <summary>
    /// 当前条目数，供检查程序验证上限。
    /// </summary>
    public static int Count => Entries.Count;

    /// <summary>
    /// 取缓存；没有时调用 <paramref name="encode"/> 编码并缓存。编码失败抛出的异常不缓存。
    /// </summary>
    /// <param name="kind">码制和影响编码结果的参数，如 <c>code128</c>、<c>qr:M</c>。</param>
    /// <param name="content">内容。</param>
    /// <param name="encode">编码函数。</param>
    public static EncodedSymbol GetOrAdd(string kind, string content, Func<string, EncodedSymbol> encode)
    {
        var key = (kind, content);
        if (Entries.TryGetValue(key, out var symbol))
        {
            RenderCaches.Symbols.Hit();
            return symbol;
        }

        RenderCaches.Symbols.Miss();
        symbol = encode(content);
        if (Entries.Count >= Capacity)
        {
            Entries.Clear();
        }

        Entries.TryAdd(key, symbol);
        return symbol;
    }
}

/// <summary>
/// 条码的几何：让模块边落在整点上。
/// </summary>
internal static class SymbolGeometry
{
    /// <summary>
    /// 旋转中心：转直角时取到半个点上，转 90° 或 270° 时再让横纵坐标的小数部分相同，这样整点的模块边转过去仍是整点；
    /// 其他角度用框的中心。
    /// </summary>
    public static SkiaSharp.SKPoint Pivot(SkiaSharp.SKRect bounds, double rotation)
    {
        var normalized = ((rotation % 360) + 360) % 360;
        if (normalized % 90 != 0)
        {
            return new SkiaSharp.SKPoint(bounds.MidX, bounds.MidY);
        }

        var x = MathF.Round(bounds.MidX * 2) / 2;
        var y = MathF.Round(bounds.MidY * 2) / 2;
        if (normalized is 90 or 270 && (x - MathF.Floor(x)) != (y - MathF.Floor(y)))
        {
            y += 0.5f;
        }

        return new SkiaSharp.SKPoint(x, y);
    }
}
