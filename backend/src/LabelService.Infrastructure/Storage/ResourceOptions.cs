namespace LabelService.Infrastructure.Storage;

/// <summary>
/// 配置节 LabelService:Assets：图标和内置图片素材的目录。上传的图片素材固定放在数据目录的 assets/ 下（ADR 0003），同名时优先于内置素材。
/// </summary>
public sealed class AssetOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Assets";

    /// <summary>
    /// 图标目录，相对路径相对于程序目录。默认 icons/（构建时从 backend/assets/icons 复制）。
    /// </summary>
    public string IconDirectory { get; set; } = "icons";

    /// <summary>
    /// 内置图片素材目录，相对路径相对于程序目录。默认 images/（构建时从 backend/assets/images 复制）。
    /// </summary>
    public string ImageDirectory { get; set; } = "images";

    /// <summary>
    /// 图标目录的绝对路径。
    /// </summary>
    /// <returns>绝对路径。</returns>
    public string ResolveIconDirectory() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, IconDirectory));

    /// <summary>
    /// 内置图片素材目录的绝对路径。
    /// </summary>
    /// <returns>绝对路径。</returns>
    public string ResolveImageDirectory() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ImageDirectory));
}

/// <summary>
/// 配置节 LabelService:Fonts：渲染用的字体目录。
/// </summary>
public sealed class FontOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Fonts";

    /// <summary>
    /// 字体目录，相对路径相对于程序目录。默认程序目录下的 fonts/（构建时从 backend/assets/fonts 复制）。
    /// </summary>
    public string Directory { get; set; } = "fonts";

    /// <summary>
    /// 字体目录的绝对路径。
    /// </summary>
    /// <returns>绝对路径。</returns>
    public string ResolveDirectory() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, Directory));
}
