using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 主窗口的模板列表：用 ListTemplatesAsync 列出这把 Key 能用的模板填进模板编码下拉框，下拉列表里同时显示名称和尺寸。
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>
    /// 下拉列表里文字的排版方式：单行、垂直居中、不把 &amp; 当快捷键。
    /// </summary>
    private const TextFormatFlags ItemTextFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

    /// <summary>
    /// 最近一次列出的模板，按编码（忽略大小写）。
    /// </summary>
    private readonly Dictionary<string, TemplateSummary> templateList = new Dictionary<string, TemplateSummary>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 绑定模板编码下拉框的事件：刷新按钮、选中后读取字段、自己画下拉列表的每一项。
    /// </summary>
    private void WireTemplateEvents()
    {
        templateCode.ItemHeight = Font.Height + Px(2);
        refreshTemplates.Click += async (_, _) => await RefreshTemplatesAsync();

        // 选中一项时下拉框的文字还没更新，等这次消息处理完再读取字段
        templateCode.SelectionChangeCommitted += (_, _) => BeginInvoke(new Action(async () => await ReadSchemaAsync()));
        templateCode.DrawItem += DrawTemplateItem;
    }

    /// <summary>
    /// 刷新模板编码下拉框：列出这把 Key 能用的已发布模板。当前填的编码保留；没填时选第一个。
    /// </summary>
    /// <returns>刷新结束（成功或失败）时完成。</returns>
    private async Task RefreshTemplatesAsync()
    {
        await RunAsync("ListTemplatesAsync", async () =>
        {
            var list = await Client().ListTemplatesAsync();
            var typed = TemplateCodeText;
            templateList.Clear();
            templateCode.BeginUpdate();
            templateCode.Items.Clear();
            foreach (var template in list)
            {
                templateList[template.TemplateCode] = template;
                templateCode.Items.Add(template.TemplateCode);
            }

            templateCode.EndUpdate();
            templateCode.Text = typed.Length == 0 && list.Count > 0 ? list[0].TemplateCode : typed;
            templateCode.DropDownWidth = Math.Max(templateCode.Width, list.Select(ItemWidth).DefaultIfEmpty(0).Max());
            schemaInfo.Text = list.Count == 0
                ? "这把 API Key 没有可用的模板：调用方没授权业务分组，或者模板还没发布"
                : templateList.ContainsKey(TemplateCodeText)
                    ? $"共 {list.Count} 个可用模板，选一个读取字段"
                    : $"共 {list.Count} 个可用模板，列表里没有 {TemplateCodeText}（没发布或没授权）";
        });
    }

    /// <summary>
    /// 启动时（不是自检时）列出可用模板，再读取当前模板的字段，表格一打开就有内容。连不上时只在状态栏和预览上方提示。
    /// </summary>
    /// <returns>做完时完成。</returns>
    private async Task LoadTemplatesOnStartAsync()
    {
        await RefreshTemplatesAsync();
        if (templateList.ContainsKey(TemplateCodeText))
        {
            await ReadSchemaAsync();
        }
    }

    /// <summary>
    /// 下拉列表的一项：编码，后面用灰色写名称、尺寸和版本。
    /// </summary>
    /// <param name="sender">下拉框。</param>
    /// <param name="e">绘制参数。</param>
    private void DrawTemplateItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index >= 0 && e.Index < templateCode.Items.Count)
        {
            var code = templateCode.Items[e.Index] as string ?? "";
            var font = e.Font ?? templateCode.Font;
            var selected = (e.State & DrawItemState.Selected) != 0;
            var codeWidth = TextRenderer.MeasureText(e.Graphics, code, font, Size.Empty, ItemTextFlags).Width;
            TextRenderer.DrawText(e.Graphics, code, font, e.Bounds, selected ? SystemColors.HighlightText : SystemColors.WindowText, ItemTextFlags);
            if (templateList.TryGetValue(code, out var template))
            {
                var rest = new Rectangle(e.Bounds.X + codeWidth + Px(10), e.Bounds.Y, Math.Max(e.Bounds.Width - codeWidth - Px(10), 0), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, Describe(template), font, rest, selected ? SystemColors.HighlightText : SystemColors.GrayText, ItemTextFlags | TextFormatFlags.EndEllipsis);
            }
        }

        e.DrawFocusRectangle();
    }

    /// <summary>
    /// 下拉列表里编码后面的说明。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <returns>如“成品箱标 · 100×60 mm · v3”。</returns>
    private static string Describe(TemplateSummary template) =>
        string.Format(CultureInfo.InvariantCulture, "{0} · {1:0.#}×{2:0.#} mm · v{3}", template.Name, template.Page.Width, template.Page.Height, template.Version);

    /// <summary>
    /// 一项完整显示需要的宽度（编码、间隔、说明、滚动条），最宽不超过 640 像素（100% 缩放时）。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <returns>像素。</returns>
    private int ItemWidth(TemplateSummary template) =>
        Math.Min(
            TextRenderer.MeasureText(template.TemplateCode, templateCode.Font, Size.Empty, ItemTextFlags).Width
            + TextRenderer.MeasureText(Describe(template), templateCode.Font, Size.Empty, ItemTextFlags).Width
            + Px(10) + SystemInformation.VerticalScrollBarWidth + Px(8),
            Px(640));
}
