namespace LabelService.Application.Resources.Dtos;

/// <summary>
/// 画布预设（设计器新建模板时可选的纸张尺寸）。
/// </summary>
/// <param name="Id">预设 ID。</param>
/// <param name="Name">名称，如“100×60 箱标”。</param>
/// <param name="Kind">类型：标签纸、办公纸张。</param>
/// <param name="WidthMm">宽度，mm。</param>
/// <param name="HeightMm">高度，mm。</param>
/// <param name="Sort">排序号，小的在前。</param>
/// <param name="Enabled">是否启用；停用的不在设计器里显示。</param>
public sealed record CanvasPresetDto(int Id, string Name, string Kind, double WidthMm, double HeightMm, int Sort, bool Enabled);

/// <summary>
/// 新建或修改画布预设。
/// </summary>
/// <param name="Name">名称，不能为空，最长 64 字。</param>
/// <param name="Kind">类型，空时为“标签纸”。</param>
/// <param name="Width">宽度，mm，0–1000，保留两位小数。</param>
/// <param name="Height">高度，mm，0–1000，保留两位小数。</param>
/// <param name="Enabled">是否启用，默认启用。</param>
public sealed record SaveCanvasPresetDto(string? Name, string? Kind, double? Width, double? Height, bool? Enabled);

/// <summary>
/// 图片素材（内置的或上传的）。模板里写 asset:Key 引用。
/// </summary>
/// <param name="Key">素材 Key。</param>
/// <param name="Name">显示名称。</param>
/// <param name="Builtin">是否内置素材（内置的不能改名、删除）。</param>
/// <param name="Size">文件大小，字节；内置素材为 0。</param>
/// <param name="CreatedAt">上传时间（UTC）；内置素材为 null。</param>
public sealed record AssetDto(string Key, string Name, bool Builtin, long Size, DateTime? CreatedAt);

/// <summary>
/// 素材改名。
/// </summary>
/// <param name="Name">新名称，不能为空，最长 128 字。</param>
public sealed record RenameAssetDto(string? Name);

/// <summary>
/// 上传的素材文件。
/// </summary>
/// <param name="FileName">原文件名（去掉扩展名后作为素材名称）。</param>
/// <param name="Length">文件大小，字节。</param>
/// <param name="Content">文件内容。</param>
public sealed record UploadAssetInput(string FileName, long Length, Stream Content);

/// <summary>
/// 素材文件（设计器显示用）。
/// </summary>
/// <param name="Content">文件内容。</param>
/// <param name="ContentType">MIME 类型：image/png、image/jpeg、image/svg+xml。</param>
/// <param name="LastModified">文件修改时间（UTC），用于浏览器缓存。</param>
public sealed record AssetFileDto(byte[] Content, string ContentType, DateTime LastModified);

/// <summary>
/// 服务端已加载的字体。
/// </summary>
/// <param name="Families">字体家族名（字体文件里的名字），按名称排序。</param>
/// <param name="Files">字体文件名。</param>
/// <param name="Items">每个字体文件的详情。</param>
/// <param name="TemplateFamilies">模板里可以写的字体名（设计器的字体下拉框）：思源黑体、思源宋体用 SourceHanSansSC 等，其余用家族名。</param>
/// <param name="HasDefaultFamily">有没有默认字体（思源黑体）；没有时没指定字体的文字用系统自带字体，中文可能显示成方框。</param>
public sealed record FontListDto(
    IReadOnlyList<string> Families,
    IReadOnlyList<string> Files,
    IReadOnlyList<FontFileDto> Items,
    IReadOnlyList<string> TemplateFamilies,
    bool HasDefaultFamily);

/// <summary>
/// 一个字体文件。
/// </summary>
/// <param name="FileName">文件名，删除时用它。</param>
/// <param name="Families">文件里的字体家族名。</param>
/// <param name="Size">文件大小，字节。</param>
/// <param name="LastModified">文件修改时间（UTC），上传的字体就是上传时间。</param>
/// <param name="Uploaded">是否在管理后台上传的（可以删除）；false 是部署目录里的字体（只读）。</param>
public sealed record FontFileDto(string FileName, IReadOnlyList<string> Families, long Size, DateTime LastModified, bool Uploaded);
