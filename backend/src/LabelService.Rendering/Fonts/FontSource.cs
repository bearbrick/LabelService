namespace LabelService.Rendering.Fonts;

/// <summary>
/// 一个字体家族来自哪个文件，以及服务端排版用的字体度量。
/// </summary>
/// <param name="Path">字体文件的完整路径。</param>
/// <param name="IsVariable">是否可变字体（有字重轴）；可变字体在浏览器里按字重范围声明，常规和粗体都用这一个文件。</param>
/// <param name="Ascent">基线以上的高度（常规字重），单位：字号。设计器用它覆盖浏览器的字体度量，基线位置才和服务端一致。</param>
/// <param name="Descent">基线以下的深度（常规字重），单位：字号。</param>
public sealed record FontSource(string Path, bool IsVariable, float Ascent, float Descent);
