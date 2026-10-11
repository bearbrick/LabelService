using System.Text;
using LabelService.Domain.Shared;

namespace LabelService.Domain.SystemSettings;

/// <summary>
/// 系统信息的规则：有哪些设置项、默认值、长度限制。系统名称显示在顶栏和浏览器标签页上，其余项显示在登录页、用户菜单里。
/// </summary>
public static class SystemInfoRules
{
    /// <summary>
    /// 系统名称，如“标签服务”。
    /// </summary>
    public const string SystemName = "system.name";

    /// <summary>
    /// 副标题，显示在系统名称旁边，如“标签设计与生成工作台”。
    /// </summary>
    public const string Subtitle = "system.subtitle";

    /// <summary>
    /// 所属公司的名称。
    /// </summary>
    public const string CompanyName = "company.name";

    /// <summary>
    /// 版权信息，如“© 2026 某某公司 版权所有”。
    /// </summary>
    public const string Copyright = "system.copyright";

    /// <summary>
    /// 技术支持的联系方式，如“信息部 分机 8001”。
    /// </summary>
    public const string Support = "system.support";

    /// <summary>
    /// 登录页左栏的大标题（宣传标语），可以换行。
    /// </summary>
    public const string LoginTitle = "login.title";

    /// <summary>
    /// 登录页左栏大标题下面的说明，可以换行。
    /// </summary>
    public const string LoginIntro = "login.intro";

    /// <summary>
    /// 公司 Logo 的文件名（数据目录 branding/ 下）；空表示没上传，界面显示占位。不在 <see cref="Fields"/> 里，由上传和恢复默认维护。
    /// </summary>
    public const string CompanyLogo = "company.logo";

    /// <summary>
    /// 可以在表单里修改的文字设置项，按页面上的顺序排列。
    /// </summary>
    public static IReadOnlyList<SystemInfoField> Fields
    {
        get;
    } =
    [
        new(SystemName, "系统名称", "标签服务", 20, true),
        new(Subtitle, "副标题", "标签设计与生成工作台", 30, false),
        new(CompanyName, "所属公司", "", 60, false),
        new(Copyright, "版权信息", "", 120, false),
        new(Support, "技术支持", "", 60, false),
        new(LoginTitle, "登录页标语", "标签和唛头在网页上设计，\n业务系统一个接口生成", 40, false, true),
        new(LoginIntro, "登录页说明", "拖拽设计模板，定义数据字段，发布后 MES、WMS、ERP 用模板编码加数据调用，拿回 PDF、PNG 或 ZPL 文件。", 200, false, true),
    ];

    /// <summary>
    /// 整理并校验一项的值：去掉首尾空白；可以换行的项保留换行（统一成 \n），其余项的换行等控制字符换成空格（显示在一行里）。
    /// </summary>
    /// <param name="field">设置项。</param>
    /// <param name="value">用户填的值；null 当作空。</param>
    /// <returns>整理后的值。</returns>
    /// <exception cref="UserFriendlyException">必填项为空，或超过长度上限。</exception>
    public static string Normalize(SystemInfoField field, string? value)
    {
        var source = (value ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
        var builder = new StringBuilder(source.Length);
        foreach (var ch in source)
        {
            builder.Append(ch == '\n' && field.Multiline ? '\n' : char.IsControl(ch) ? ' ' : ch);
        }

        var text = builder.ToString().Trim();
        if (field.Required && text.Length == 0)
        {
            throw new UserFriendlyException($"{field.Label}不能为空");
        }

        return text.Length > field.MaxLength ? throw new UserFriendlyException($"{field.Label}最长 {field.MaxLength} 字") : text;
    }
}

/// <summary>
/// 一个文字设置项的定义。
/// </summary>
/// <param name="Key">键，如 system.name，存在 system_setting 表的 key 列。</param>
/// <param name="Label">中文名称，校验失败的提示里用。</param>
/// <param name="Default">没保存过时的默认值。</param>
/// <param name="MaxLength">最长多少字。</param>
/// <param name="Required">是否必填。</param>
/// <param name="Multiline">是否可以换行（登录页的标语和说明）。</param>
public sealed record SystemInfoField(string Key, string Label, string Default, int MaxLength, bool Required, bool Multiline = false);
