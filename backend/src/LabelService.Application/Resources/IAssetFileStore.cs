using LabelService.Application.Resources.Dtos;

namespace LabelService.Application.Resources;

/// <summary>
/// 素材文件的存放：上传的素材放在数据目录的 assets/（文件名就是素材 Key + 扩展名），内置素材在程序目录的 images/。
/// 基础设施层实现。
/// </summary>
public interface IAssetFileStore
{
    /// <summary>
    /// 保存上传的素材文件。
    /// </summary>
    /// <param name="fileName">文件名，如 a1b2c3d4e5.png。</param>
    /// <param name="content">文件内容。</param>
    /// <returns>写完时完成。</returns>
    Task SaveAsync(string fileName, byte[] content);

    /// <summary>
    /// 删除上传的素材文件；文件不存在时什么也不做。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    void Delete(string fileName);

    /// <summary>
    /// 按 Key 找素材文件：先找上传目录，再找内置目录；扩展名依次试 .png、.jpg、.svg。
    /// </summary>
    /// <param name="key">素材 Key（调用前已确认只含字母、数字、下划线、连字符）。</param>
    /// <returns>文件内容、类型和修改时间；找不到时为 null。</returns>
    AssetFileDto? Find(string key);
}
