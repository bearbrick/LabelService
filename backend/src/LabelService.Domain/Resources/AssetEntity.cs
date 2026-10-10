namespace LabelService.Domain.Resources;

/// <summary>
/// 表 asset：上传的素材的元数据。文件放在数据目录的 assets/ 下，表里只存元数据。
/// </summary>
public sealed class AssetEntity
{
    /// <summary>
    /// 主键。
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// 种类：image、icon、font。
    /// </summary>
    public string Kind { get; set; } = "";

    /// <summary>
    /// 显示名称。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 素材 Key：模板里写 asset:Key；也是数据目录 assets/ 下的文件名（不含扩展名）。
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 文件路径，相对于数据目录下的 assets/。
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// 文件内容的 SHA-256（小写十六进制），用于去重和校验。
    /// </summary>
    public string Sha256 { get; set; } = "";

    /// <summary>
    /// 文件大小，字节。
    /// </summary>
    public long Size
    {
        get; set;
    }

    /// <summary>
    /// 上传时间（UTC）。
    /// </summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}
