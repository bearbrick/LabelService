using LabelService.Application.Resources.Dtos;

namespace LabelService.Application.Resources;

/// <summary>
/// 字体（登录即可看）：服务端已加载的字体家族和文件，资源管理页显示用。
/// </summary>
public interface IFontAppService
{
    /// <summary>
    /// 已加载的字体。
    /// </summary>
    /// <returns>字体家族名和文件名。</returns>
    FontListDto List();
}
