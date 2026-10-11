using System.Collections.Concurrent;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Diagnostics;
using SkiaSharp;
using Svg.Skia;

namespace LabelService.Rendering.Assets;

/// <summary>
/// 素材库：图标（GB/T 191 等 SVG）和图片素材（PNG、JPG、SVG），服务启动时从目录加载进内存，渲染时只查内存（R3）。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>图标按相对路径命名，如 <c>icons/gb191/fragile.svg</c> 是 <c>gb191/fragile</c>；图片按文件名（不含扩展名）命名，模板里写 <c>asset:文件名</c>。</item>
/// <item>SVG 先经 <see cref="SvgSanitizer"/> 清洗，再用 Svg.Skia 转成 SKPicture。</item>
/// <item>图片按目标尺寸、fit、mono 栅格化后缓存，同一素材同一尺寸只缩放一次；缓存有上限，超过时清空。</item>
/// <item>上传、删除素材后调 <see cref="ReloadImages"/> 整体替换图片表（读不加锁），缓存随版本失效。</item>
/// </list>
/// 加载完成后只读（缩放缓存线程安全），多线程共用。
/// </remarks>
public sealed class AssetLibrary : IDisposable
{
    /// <summary>
    /// 缩放缓存的上限（条）。
    /// </summary>
    public const int ScaledCacheCapacity = 512;

    /// <summary>
    /// 认的图片素材扩展名。
    /// </summary>
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".svg"];

    /// <summary>
    /// 图标名（如 gb191/fragile）→ 矢量图。启动后不变。
    /// </summary>
    private readonly Dictionary<string, SKPicture> icons;

    /// <summary>
    /// 图片素材目录，按优先级排列（上传的在前，内置的在后），重新加载时用。
    /// </summary>
    private readonly IReadOnlyList<string> imageDirectories;

    /// <summary>
    /// 缩放缓存：素材 ID、图片表版本、目标像素尺寸、fit、是否黑白 → 栅格化好的图。
    /// </summary>
    private readonly ConcurrentDictionary<(string Id, int Version, int Width, int Height, ImageFit Fit, bool Mono), SKImage> scaled = new();

    /// <summary>
    /// 当前图片表：素材 ID → 图片源。重新加载时整体替换，读不加锁。
    /// </summary>
    private volatile Dictionary<string, ImageSource> images;

    /// <summary>
    /// 图片表的版本，每次重新加载加一，旧版本的缩放缓存因此失效。
    /// </summary>
    private int version;

    /// <summary>
    /// 由 <see cref="Load"/> 创建。
    /// </summary>
    /// <param name="icons">图标。</param>
    /// <param name="images">图片素材。</param>
    /// <param name="imageDirectories">图片素材目录（重新加载时用）。</param>
    /// <param name="errors">加载失败的文件和原因。</param>
    private AssetLibrary(Dictionary<string, SKPicture> icons, Dictionary<string, ImageSource> images, IReadOnlyList<string> imageDirectories, IReadOnlyList<string> errors)
    {
        this.icons = icons;
        this.images = images;
        this.imageDirectories = imageDirectories;
        Errors = errors;
    }

    /// <summary>
    /// 没有任何素材的空库。
    /// </summary>
    public static AssetLibrary Empty { get; } = new([], [], [], []);

    /// <summary>
    /// 已加载的图标名，如 gb191/fragile。
    /// </summary>
    public IReadOnlyCollection<string> IconNames => icons.Keys;

    /// <summary>
    /// 已加载的图片素材 ID。
    /// </summary>
    public IReadOnlyCollection<string> ImageIds => images.Keys;

    /// <summary>
    /// 加载失败的文件和原因，由调用方写日志。
    /// </summary>
    public IReadOnlyList<string> Errors
    {
        get;
    }

    /// <summary>
    /// 缩放缓存里的条数，供检查程序验证缓存生效。
    /// </summary>
    public int ScaledCount => scaled.Count;

    /// <summary>
    /// 加载图标目录（含子目录）和若干图片目录（不含子目录）。目录不存在时跳过；同一个图片 ID 以先加载的为准。只在启动时调用。
    /// </summary>
    /// <param name="iconDirectory">图标目录；null 表示没有图标。</param>
    /// <param name="imageDirectories">图片素材目录，排在前面的优先（上传的素材放前面，覆盖同名的内置素材）。</param>
    /// <returns>素材库；加载失败的文件记在 <see cref="Errors"/> 里。</returns>
    public static AssetLibrary Load(string? iconDirectory, IEnumerable<string> imageDirectories)
    {
        var icons = new Dictionary<string, SKPicture>(StringComparer.Ordinal);
        var errors = new List<string>();
        if (iconDirectory is not null && Directory.Exists(iconDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(iconDirectory, "*.svg", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var name = Path.ChangeExtension(Path.GetRelativePath(iconDirectory, path), null).Replace('\\', '/');
                try
                {
                    icons[name] = LoadSvg(File.ReadAllText(path));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException)
                {
                    errors.Add($"图标 {path} 加载失败：{ex.Message}");
                }
            }
        }

        var directories = imageDirectories.ToList();
        return new AssetLibrary(icons, LoadImages(directories, errors), directories, errors);
    }

    /// <summary>
    /// 重新加载图片素材（上传、删除素材后调用）：整体替换图片表，正在渲染的请求不受影响。
    /// </summary>
    /// <returns>加载失败的文件和原因。</returns>
    public IReadOnlyList<string> ReloadImages()
    {
        var errors = new List<string>();
        images = LoadImages(imageDirectories, errors);
        Interlocked.Increment(ref version);
        scaled.Clear();
        return errors;
    }

    /// <summary>
    /// 读取图片目录里的 PNG、JPG、SVG；同一个 ID（文件名不含扩展名）只认第一个读到的。
    /// </summary>
    /// <param name="imageDirectories">图片素材目录，排在前面的优先。</param>
    /// <param name="errors">加载失败的文件和原因，往里追加。</param>
    /// <returns>素材 ID → 图片源。</returns>
    private static Dictionary<string, ImageSource> LoadImages(IEnumerable<string> imageDirectories, List<string> errors)
    {
        var images = new Dictionary<string, ImageSource>(StringComparer.Ordinal);
        foreach (var directory in imageDirectories.Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();
                var id = Path.GetFileNameWithoutExtension(path);
                if (!ImageExtensions.Contains(extension) || images.ContainsKey(id))
                {
                    continue;
                }

                try
                {
                    images[id] = extension == ".svg"
                        ? new ImageSource(null, LoadSvg(File.ReadAllText(path)))
                        : new ImageSource(SKImage.FromEncodedData(File.ReadAllBytes(path)) ?? throw new InvalidDataException("不是有效的 PNG 或 JPG"), null);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException)
                {
                    errors.Add($"图片素材 {path} 加载失败：{ex.Message}");
                }
            }
        }

        return images;
    }

    /// <summary>
    /// 释放图片和缓存。
    /// </summary>
    public void Dispose()
    {
        foreach (var picture in icons.Values)
        {
            picture.Dispose();
        }

        foreach (var image in images.Values)
        {
            image.Raster?.Dispose();
            image.Vector?.Dispose();
        }

        foreach (var image in scaled.Values)
        {
            image.Dispose();
        }
    }

    /// <summary>
    /// 按名字取图标。
    /// </summary>
    /// <param name="name">图标名，如 gb191/fragile。</param>
    /// <param name="picture">找到的矢量图。</param>
    /// <returns>找到时为 true。</returns>
    internal bool TryGetIcon(string name, out SKPicture picture) => icons.TryGetValue(name, out picture!);

    /// <summary>
    /// 取图片素材（当前图片表的快照，之后的查询都用它，不受并发重新加载影响）。
    /// </summary>
    /// <param name="id">素材 ID（模板里 asset: 后面的部分）。</param>
    /// <param name="source">找到的图片源。</param>
    /// <returns>找到时为 true。</returns>
    internal bool TryGetImage(string id, out ImageSource source) => images.TryGetValue(id, out source!);

    /// <summary>
    /// 取图片素材按目标尺寸栅格化的结果，带缓存。
    /// </summary>
    /// <param name="id">素材 ID。</param>
    /// <param name="source">图片源。</param>
    /// <param name="width">目标宽度，像素（打印点）。</param>
    /// <param name="height">目标高度，像素。</param>
    /// <param name="fit">缩放方式。</param>
    /// <param name="mono">是否转成黑白（抖动）。</param>
    /// <returns>栅格化好的图，缓存共用，不要释放。</returns>
    internal SKImage Scaled(string id, ImageSource source, int width, int height, ImageFit fit, bool mono)
    {
        var key = (id, Volatile.Read(ref version), width, height, fit, mono);
        if (scaled.TryGetValue(key, out var image))
        {
            RenderCaches.Images.Hit();
            return image;
        }

        RenderCaches.Images.Miss();
        image = ImageRaster.Render(source, width, height, fit, mono);
        if (scaled.Count >= ScaledCacheCapacity)
        {
            // 清空时不释放旧图：别的线程可能正在画。交给 GC。
            scaled.Clear();
        }

        var stored = scaled.GetOrAdd(key, image);
        if (!ReferenceEquals(stored, image))
        {
            // 别的线程先放进去了，用它的。
            image.Dispose();
        }

        return stored;
    }

    /// <summary>
    /// 清洗后用 Svg.Skia 转成 SKPicture，再录成自己的 SKPicture（Svg.Skia 的 Picture 跟着 SKSvg 一起释放）。
    /// </summary>
    /// <param name="svg">SVG 文本。</param>
    /// <returns>矢量图。</returns>
    /// <exception cref="InvalidDataException">SVG 没有可画的内容。</exception>
    internal static SKPicture LoadSvg(string svg)
    {
        using var loader = new SKSvg();
        var picture = loader.FromSvg(SvgSanitizer.Sanitize(svg)) ?? throw new InvalidDataException("SVG 没有可画的内容");
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(picture.CullRect);
        canvas.DrawPicture(picture);
        return recorder.EndRecording();
    }
}

/// <summary>
/// 图片源：位图或矢量图，二选一。
/// </summary>
/// <param name="Raster">位图（PNG、JPG）；矢量图时为 null。</param>
/// <param name="Vector">矢量图（SVG）；位图时为 null。</param>
internal sealed record ImageSource(SKImage? Raster, SKPicture? Vector)
{
    /// <summary>
    /// 原始尺寸。
    /// </summary>
    public SKSize Size => Raster is not null ? new SKSize(Raster.Width, Raster.Height) : Vector!.CullRect.Size;
}
