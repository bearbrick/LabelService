using System.ComponentModel;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
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
    /// 在后台提交当前完整结果，PNG ZIP、PDF 多页都作为完整作业打印。
    /// </summary>
    /// <returns>提交完成或已显示错误后结束。</returns>
    private async Task PrintResultAsync()
    {
        var file = result;
        if (file is null || printing)
        {
            return;
        }

        printing = true;
        printMenu.Enabled = false;
        status.Text = "正在提交到默认打印机…";
        try
        {
            // 生成结果在开始时固定，期间切换模板或重新生成不会替换正在打印的内容。
            await Task.Run(() => WindowsLabelPrinter.Print(file));
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
            }
        }
    }
}
