using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Net.Sockets;
using System.Windows.Forms;
using LabelService.Client;
using LabelService.Client.Printing;

namespace LabelService.Demo;

/// <summary>
/// “发送到打印机”对话框：ZPL 发网络打印机（TCP 9100，<see cref="NetworkPrinter"/>）或以 RAW 方式写 Windows 打印队列
/// （<see cref="WindowsRawPrinter"/>）；PNG 用 PrintDocument 按 DPI 原尺寸打印；PDF 交给默认程序。
/// </summary>
internal sealed class PrintForm : Form
{
    private readonly LabelFile file;
    private readonly Image? image;
    private readonly RadioButton network = new RadioButton { Text = "网络打印机（ZPL 原始数据，TCP 9100）", AutoSize = true };
    private readonly RadioButton windows = new RadioButton { Text = "Windows 打印机", AutoSize = true };
    private readonly TextBox host = new TextBox { Text = "192.168.1.50", Width = 160 };
    private readonly NumericUpDown port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 9100, Width = 70 };
    private readonly ComboBox printer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly Button send = new Button { Text = "发送", AutoSize = true, DialogResult = DialogResult.None };

    public PrintForm(LabelFile file, Image? image)
    {
        this.file = file;
        this.image = image;
        Text = "发送到打印机";
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
            printer.SelectedIndex = 0;
        }

        var isZpl = file.Format == LabelFormat.Zpl && !file.IsZip;
        var isPng = file.Format == LabelFormat.Png && !file.IsZip;
        network.Enabled = isZpl;
        windows.Enabled = isZpl || isPng;
        network.Checked = isZpl;
        windows.Checked = !isZpl && isPng;
        var note = isZpl ? "ZPL 用 WindowsRawPrinter 以 RAW 方式写入打印队列，适合 USB 连接的标签打印机。"
            : isPng ? $"PNG 用 PrintDocument 按 {file.Dpi} dpi 原尺寸打印。"
            : file.Format == LabelFormat.Pdf ? "PDF 不能直接发给标签打印机：用“用默认程序打开”，在 PDF 阅读器里打印。"
            : "压缩包要先解开再逐张打印。";

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

    private async Task SendAsync()
    {
        send.Enabled = false;
        try
        {
            if (network.Checked)
            {
                await NetworkPrinter.SendAsync(host.Text.Trim(), (int)port.Value, file.Content, TimeSpan.FromSeconds(5));
            }
            else if (file.Format == LabelFormat.Zpl)
            {
                WindowsRawPrinter.Send((string)printer.SelectedItem!, file.Content, file.FileName);
            }
            else
            {
                PrintImage((string)printer.SelectedItem!);
            }

            MessageBox.Show(this, "已发送到打印机。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or Win32Exception or InvalidPrinterException or InvalidOperationException)
        {
            MessageBox.Show(this, "发送失败：" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            send.Enabled = true;
        }
    }

    /// <summary>
    /// 按标签的实际尺寸打印 PNG：像素 ÷ DPI = 英寸，PrintDocument 的单位是百分之一英寸。
    /// </summary>
    private void PrintImage(string printerName)
    {
        if (image is null)
        {
            throw new InvalidOperationException("没有可打印的图片");
        }

        var dpi = file.Dpi > 0 ? file.Dpi : 300;
        var width = image.Width * 100 / dpi;
        var height = image.Height * 100 / dpi;
        using var document = new PrintDocument { DocumentName = file.FileName };
        document.PrinterSettings.PrinterName = printerName;
        document.DefaultPageSettings.PaperSize = new PaperSize("Label", width, height);
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.PrintPage += (_, e) =>
        {
            e.Graphics!.DrawImage(image, new Rectangle(0, 0, width, height));
            e.HasMorePages = false;
        };
        document.Print();
    }

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        flow.Controls.AddRange(controls);
        return flow;
    }
}
