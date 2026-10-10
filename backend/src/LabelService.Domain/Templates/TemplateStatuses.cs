namespace LabelService.Domain.Templates;

/// <summary>
/// 模板和模板版本的状态取值。
/// </summary>
public static class TemplateStatuses
{
    /// <summary>
    /// 草稿：模板从未发布过，或版本还没发布。
    /// </summary>
    public const string Draft = "draft";

    /// <summary>
    /// 已发布：模板至少发布过一个版本，或版本已发布。
    /// </summary>
    public const string Published = "published";

    /// <summary>
    /// 已删除（只用于模板）：不再出现在列表里，开放接口不能再用；版本记录保留备查，编码不能再用。
    /// </summary>
    public const string Deleted = "deleted";
}
