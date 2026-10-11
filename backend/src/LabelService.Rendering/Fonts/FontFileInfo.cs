namespace LabelService.Rendering.Fonts;

/// <summary>
/// 一个已加载的字体文件，给管理后台的“素材与字体”页显示。
/// </summary>
/// <param name="Path">文件的绝对路径。</param>
/// <param name="Families">文件里的字体家族名（字体集合里可能有多个）。</param>
/// <param name="Size">文件大小，字节。</param>
/// <param name="LastModified">文件修改时间（UTC），上传的字体就是上传时间。</param>
/// <param name="Uploaded">是否在管理后台上传的（在数据目录里，可以删除）；false 是部署目录里的字体。</param>
public sealed record FontFileInfo(string Path, IReadOnlyList<string> Families, long Size, DateTime LastModified, bool Uploaded);
