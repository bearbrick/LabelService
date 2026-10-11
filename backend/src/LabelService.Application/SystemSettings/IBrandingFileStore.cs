using LabelService.Application.Resources.Dtos;

namespace LabelService.Application.SystemSettings;

/// <summary>
/// 公司 Logo 文件的存放：放在数据目录的 branding/ 下，文件名由应用服务生成（带内容摘要，换图后地址也跟着变）。
/// 基础设施层实现。
/// </summary>
public interface IBrandingFileStore
{
    /// <summary>
    /// 保存 Logo 文件。
    /// </summary>
    /// <param name="fileName">文件名，如 company-logo-1a2b3c4d5e6f.png。</param>
    /// <param name="content">文件内容。</param>
    /// <returns>写完时完成。</returns>
    Task SaveAsync(string fileName, byte[] content);

    /// <summary>
    /// 删除 Logo 文件；文件不存在或文件名不合法时什么也不做。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    void Delete(string fileName);

    /// <summary>
    /// 读取 Logo 文件。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <returns>文件内容、类型和修改时间；文件不存在或文件名不合法时为 null。</returns>
    AssetFileDto? Find(string fileName);
}
