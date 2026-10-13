using System.Windows.Forms;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 保留已经生成的业务条目或拼版纸页，选择补打时不读取当前业务输入、不重新生成。
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>
    /// 当前批量集合；生成单张、发生错误或清空结果时移除。
    /// </summary>
    private LabelBatchResult? batchResult;

    /// <summary>
    /// 已生成结果的条目选择，编号从一开始显示。
    /// </summary>
    private readonly ComboBox printItem = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };

    /// <summary>
    /// 单项补打按钮，不要求再次选择打印机。
    /// </summary>
    private readonly Button printSelected = new Button { Text = "静默打印所选项", AutoSize = true, Enabled = false };

    /// <summary>
    /// 集合专用工具栏，单张生成时隐藏。
    /// </summary>
    private Control? batchPrintTools;

    /// <summary>
    /// 从缩略图同步选择时，保留当前查看的份数页，避免跳回该条的第一份。
    /// </summary>
    private bool syncingPrintItem;

    /// <summary>
    /// 建立独立一行，避免把选择框挤在保存和缩放工具栏中。
    /// </summary>
    /// <returns>工具栏。</returns>
    private Control BuildBatchPrintTools()
    {
        printSelected.Click += async (_, _) => await PrintResultAsync(selectedOnly: true);
        printItem.SelectedIndexChanged += (_, _) => PreviewPrintItem();
        batchPrintTools = Toolbar(new Label { Text = "已生成项", AutoSize = true, Margin = new Padding(3, 7, 3, 0) },
            printItem, printSelected);
        batchPrintTools.Visible = false;
        return batchPrintTools;
    }

    /// <summary>
    /// 先显示原始结果，再装入可补打集合；预览数量限制不影响打印选择数量。
    /// </summary>
    /// <param name="batch">已生成集合。</param>
    private void ShowBatchResult(LabelBatchResult batch)
    {
        ShowResult(batch.OriginalFile);
        batchResult = batch;
        foreach (var item in batch.Items)
        {
            printItem.Items.Add(batch.Unit == LabelBatchUnit.Page ? $"第 {item.PageNumber} 页（拼版纸张）"
                : $"第 {item.ItemNumber} 条（{item.Copies} 份）");
        }
        printItem.SelectedIndex = 0;
        printSelected.Enabled = !printing;
        if (batchPrintTools is not null)
        {
            batchPrintTools.Visible = true;
        }
    }

    /// <summary>
    /// 清除旧的补打选择，防止单张结果或错误后打印上一次的批量内容。
    /// </summary>
    private void ClearBatchResult()
    {
        batchResult = null;
        printItem.Items.Clear();
        printSelected.Enabled = false;
        if (batchPrintTools is not null)
        {
            batchPrintTools.Visible = false;
        }
    }

    /// <summary>
    /// 选择补打项时预览该项第一份，超出预览限制时明确提示，不显示另一项的图片。
    /// </summary>
    private void PreviewPrintItem()
    {
        if (syncingPrintItem || batchResult is null || printItem.SelectedIndex < 0 || images.Count == 0)
        {
            return;
        }
        var index = batchResult.Items.Take(printItem.SelectedIndex).Sum(item => item.Files.Count);
        if (index < images.Count)
        {
            current = index;
            SetPreview(Picture());
        }
        else
        {
            SetPreview(Message("该项未加载预览图，仍可直接打印已经生成的内容。"));
        }
    }

    /// <summary>
    /// 点击缩略图时同步业务条目或纸页选择；同一条目的任一份都对应同一个打印项。
    /// </summary>
    /// <param name="index">预览图片索引。</param>
    private void TrackPreviewSelection(int index)
    {
        if (batchResult is null)
        {
            return;
        }
        syncingPrintItem = true;
        try
        {
            var offset = 0;
            for (var item = 0; item < batchResult.Items.Count; item++)
            {
                offset += batchResult.Items[item].Files.Count;
                if (index < offset)
                {
                    printItem.SelectedIndex = item;
                    break;
                }
            }
            // 从超出预览范围的文字提示返回图片时，重新建立图片控件。
            if (!previewHost.Controls.OfType<PictureBox>().Any())
            {
                SetPreview(Picture());
            }
        }
        finally { syncingPrintItem = false; }
    }
}
