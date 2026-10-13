using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LabelService.Client;
using LabelService.Client.Printing;

namespace LabelService.Demo;

/// <summary>
/// 特殊情况下选择其他打印机：三种格式使用统一静默入口，非 ZIP 的 ZPL 也可直连网络。
/// </summary>
internal sealed class PrintForm : Form
{
    /// <summary>
    /// 要打印的结果。
    /// </summary>
    private readonly LabelFile file;

    /// <summary>
    /// 发到网络打印机（只有 ZPL 可用）。
    /// </summary>
    private readonly RadioButton network = new RadioButton { Text = "网络打印机（ZPL 原始数据，TCP 9100）", AutoSize = true };

    /// <summary>
    /// 发到 Windows 打印机，默认不修改驱动参数。
    /// </summary>
    private readonly RadioButton windows = new RadioButton { Text = "Windows 打印机", AutoSize = true };

    /// <summary>
    /// 网络打印机的地址。
    /// </summary>
    private readonly TextBox host = new TextBox { Text = "192.168.1.50", Width = 160 };

    /// <summary>
    /// 网络打印机的端口，默认 9100。
    /// </summary>
    private readonly NumericUpDown port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 9100, Width = 70 };

    /// <summary>
    /// 本机安装的 Windows 打印机。
    /// </summary>
    private readonly ComboBox printer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };

    /// <summary>
    /// 发送按钮（发送中禁用，失败后恢复）。
    /// </summary>
    private readonly Button send = new Button { Text = "发送", AutoSize = true, DialogResult = DialogResult.None };

    /// <summary>
    /// 按结果的格式决定能选哪种打印方式，并给出说明。
    /// </summary>
    /// <param name="file">要打印的结果。</param>
    public PrintForm(LabelFile file)
    {
        this.file = file;

        // 和主窗口一样按屏幕缩放：下面的尺寸都是 100% 缩放时的像素
        FormScaling.Begin(this);
        Text = "选择其他打印机";
        Icon = DemoIcon.Value;
        Font = new Font("Microsoft YaHei UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            printer.Items.Add(name);
        }

        if (printer.Items.Count > 0)
        {
            var defaultName = new PrinterSettings().PrinterName;
            printer.SelectedIndex = Math.Max(0, printer.Items.IndexOf(defaultName));
        }

        var isZpl = file.Format == LabelFormat.Zpl && !file.IsZip;
        network.Enabled = isZpl;
        windows.Enabled = printer.Items.Count > 0;
        windows.Checked = windows.Enabled;
        network.Checked = !windows.Enabled && isZpl;
        var note = file.Format == LabelFormat.Zpl
            ? "ZPL 直接提交原始指令，打印机必须支持 ZPL；DPI 和份数由生成请求决定。"
            : "PNG、PDF 及多页 ZIP 按实际尺寸打印，沿用驱动已配置的纸张和方向。PDF 不需要阅读器。";

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        layout.Controls.Add(network);
        layout.Controls.Add(Flow(new Label { Text = "地址", AutoSize = true, Margin = new Padding(20, 7, 3, 0) }, host, new Label { Text = "端口", AutoSize = true, Margin = new Padding(8, 7, 3, 0) }, port));
        layout.Controls.Add(windows);
        layout.Controls.Add(Flow(new Label { Text = "打印机", AutoSize = true, Margin = new Padding(20, 7, 3, 0) }, printer));
        layout.Controls.Add(new Label { Text = note, AutoSize = true, MaximumSize = new Size(440, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 10, 3, 10) });
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = Flow(send, cancel);
        buttons.Anchor = AnchorStyles.Right;
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        FormScaling.End(this);
        AcceptButton = send;
        CancelButton = cancel;
        send.Enabled = network.Enabled || windows.Enabled;

        void Sync()
        {
            host.Enabled = port.Enabled = network.Checked;
            printer.Enabled = windows.Checked;
        }

        network.CheckedChanged += (_, _) => Sync();
        windows.CheckedChanged += (_, _) => Sync();
        Sync();
        send.Click += async (_, _) => await SendAsync();
    }

    /// <summary>
    /// 按选中的方式发送；打印机、网络错误弹框提示，可以改设置再发。
    /// </summary>
    /// <returns>发送结束时完成。</returns>
    private async Task SendAsync()
    {
        send.Enabled = false;
        try
        {
            if (network.Checked)
            {
                await NetworkPrinter.SendAsync(host.Text.Trim(), (int)port.Value, file.Content, TimeSpan.FromSeconds(5));
            }
            else
            {
                var name = (string)printer.SelectedItem!;
                await Task.Run(() => WindowsLabelPrinter.Print(file, name));
            }

            if (IsDisposed || Disposing)
            {
                return;
            }

            MessageBox.Show(this, "打印任务已提交，请在打印队列查看状态。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or Win32Exception or InvalidPrinterException
            or InvalidOperationException or COMException or IOException or ArgumentException or PlatformNotSupportedException)
        {
            if (!IsDisposed && !Disposing)
            {
                MessageBox.Show(this, "发送失败：" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                send.Enabled = true;
            }
        }
    }

    /// <summary>
    /// 横排一组控件。
    /// </summary>
    /// <param name="controls">控件。</param>
    /// <returns>横排面板。</returns>
    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        flow.Controls.AddRange(controls);
        return flow;
    }
}
