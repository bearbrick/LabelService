namespace LabelService.Application.Resources;

/// <summary>
/// 上传字体文件的存放：数据目录的 fonts/（和素材、Logo 一样跟着数据目录备份和迁移，ADR 0011）。基础设施层实现。
/// </summary>
public interface IFontFileStore
{
    /// <summary>
    /// 保存上传的字体文件。
    /// </summary>
    /// <param name="fileName">文件名（调用前已确认只含安全的字符）。</param>
    /// <param name="content">文件内容。</param>
    /// <returns>写完时完成。</returns>
    Task SaveAsync(string fileName, byte[] content);

    /// <summary>
    /// 删除上传的字体文件；文件不存在时什么也不做。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    void Delete(string fileName);
}
