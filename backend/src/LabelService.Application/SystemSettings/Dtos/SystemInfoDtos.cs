namespace LabelService.Application.SystemSettings.Dtos;

/// <summary>
/// 系统信息：顶栏、登录页、浏览器标签页标题、用户菜单显示的内容。登录前也能读取。
/// </summary>
/// <param name="SystemName">系统名称，不会为空。</param>
/// <param name="Subtitle">副标题；空表示不显示。</param>
/// <param name="CompanyName">所属公司；空表示不显示。</param>
/// <param name="Copyright">版权信息；空表示不显示。</param>
/// <param name="Support">技术支持的联系方式；空表示不显示。</param>
/// <param name="LoginTitle">登录页左栏的大标题，可以有换行（\n）。</param>
/// <param name="LoginIntro">登录页大标题下面的说明，可以有换行（\n）；空表示不显示。</param>
/// <param name="LogoVersion">完整 Logo 的版本（换一次变一次，拼在图片地址后面让浏览器重新下载）；没上传时为 null。</param>
/// <param name="IconVersion">Logo 图标（顶栏、浏览器标签页用）的版本；没上传时为 null，界面改用完整 Logo，都没有时显示占位。</param>
/// <param name="LogoDarkVersion">深色背景 Logo（登录页深蓝底上直接显示）的版本；没上传时为 null，登录页用白底卡片放完整 Logo。</param>
public sealed record SystemInfoDto(
    string SystemName,
    string Subtitle,
    string CompanyName,
    string Copyright,
    string Support,
    string LoginTitle,
    string LoginIntro,
    string? LogoVersion,
    string? IconVersion,
    string? LogoDarkVersion);

/// <summary>
/// 修改系统信息的文字部分（Logo 单独上传）。每项都会去掉首尾空白，除登录页的标语和说明外换行换成空格；
/// 某项不传（null）表示不改，传空字符串表示清空。
/// </summary>
/// <param name="SystemName">系统名称，必填，最长 20 字。</param>
/// <param name="Subtitle">副标题，最长 30 字。</param>
/// <param name="CompanyName">所属公司，最长 60 字。</param>
/// <param name="Copyright">版权信息，最长 120 字。</param>
/// <param name="Support">技术支持，最长 60 字。</param>
/// <param name="LoginTitle">登录页标语，可以换行，最长 40 字。</param>
/// <param name="LoginIntro">登录页说明，可以换行，最长 200 字。</param>
public sealed record SaveSystemInfoDto(string? SystemName, string? Subtitle, string? CompanyName, string? Copyright, string? Support, string? LoginTitle = null, string? LoginIntro = null);
