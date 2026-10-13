using System.ComponentModel;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LabelService.Client;
using LabelService.Client.Printing;

namespace LabelService.Demo;

/// <summary>
/// 演示程序的常规静默打印：沿用系统默认打印机，不要求用户选择或重新配置。
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>
    /// 正在提交打印，避免连续点击生成重复作业。
    /// </summary>
    private bool printing;

    /// <summary>
    /// 在后台提交完整结果或当前选中项，使用已经生成的集合。
    /// </summary>
    /// <param name="selectedOnly">是否只提交选择框中的条目或纸页。</param>
    /// <returns>提交完成或已显示错误后结束。</returns>
    private async Task PrintResultAsync(bool selectedOnly = false)
    {
        var file = result;
        if (file is null || printing)
        {
            return;
        }

        IReadOnlyList<LabelBatchItem>? items = batchResult?.Items;
        if (selectedOnly)
        {
            if (items is null || printItem.SelectedIndex < 0)
            {
                return;
            }
            items = new[] { items[printItem.SelectedIndex] };
        }

        printing = true;
        printMenu.Enabled = false;
        printSelected.Enabled = false;
        status.Text = "正在提交到默认打印机…";
        try
        {
            // 生成结果在开始时固定，期间切换模板或重新生成不会替换正在打印的内容。
            await Task.Run(() =>
            {
                if (items is null)
                {
                    WindowsLabelPrinter.Print(file);
                }
                else
                {
                    WindowsLabelPrinter.Print(items);
                }
            });
            if (!IsDisposed && !Disposing)
            {
                status.Text = "已提交到 Windows 默认打印机，请在打印队列查看状态。";
            }
        }
        catch (Exception exception) when (exception is InvalidPrinterException or Win32Exception or COMException
            or IOException or InvalidOperationException or ArgumentException or PlatformNotSupportedException or TimeoutException)
        {
            if (!IsDisposed && !Disposing)
            {
                status.Text = "打印提交失败：" + exception.Message;
                MessageBox.Show(this, exception.Message, "打印提交失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            printing = false;
            if (!IsDisposed && !Disposing)
            {
                printMenu.Enabled = result is not null;
                printSelected.Enabled = batchResult is not null;
            }
        }
    }
}
