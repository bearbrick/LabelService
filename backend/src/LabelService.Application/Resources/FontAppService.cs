using LabelService.Application.Resources.Dtos;
using LabelService.Rendering.Fonts;

namespace LabelService.Application.Resources;

/// <summary>
/// 字体的应用服务实现。
/// </summary>
/// <param name="fonts">渲染用的字体注册表。</param>
public sealed class FontAppService(FontRegistry fonts) : IFontAppService
{
    /// <inheritdoc />
    public FontListDto List() => new(
        fonts.FamilyNames.Order(StringComparer.Ordinal).ToArray(),
        fonts.Files.Select(path => Path.GetFileName(path)).ToArray());
}
