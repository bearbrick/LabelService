using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows.Forms;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 最小可用的演示窗体：填连接和数据，生成单张标签，显示结果和调用日志。
/// 完整功能（批量、打印、调用代码、模拟服务）见 docs/tasks/T-012。
/// </summary>
internal sealed class MainForm : Form
{
    private static readonly JsonSerializerOptions PrettyJson = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly TextBox baseAddress = new TextBox { Text = "http://localhost:5080/", Dock = DockStyle.Fill };
    private readonly TextBox apiKey = new TextBox { Text = "lbl_test_local_dev", UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly TextBox templateCode = new TextBox { Text = "BOX_LABEL", Dock = DockStyle.Fill };
    private readonly ComboBox format = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox data = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9.5f) };
    private readonly Button loadSchema = new Button { Text = "读取字段", AutoSize = true };
    private readonly Button render = new Button { Text = "生成单张", AutoSize = true };
    private readonly PictureBox preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(0xF3, 0xF5, 0xF8) };
    private readonly TextBox log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };

    private LabelServiceClient? client;
    private string clientSettings = "";

    public MainForm()
    {
        Text = "标签服务 SDK 调用演示 - LabelServiceDemo";
        Font = new Font("Microsoft YaHei UI", 9f);
        ClientSize = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterScreen;
        format.Items.AddRange(new object[] { "png", "pdf", "zpl" });
        format.SelectedIndex = 0;
        data.Text = "{\r\n  \"partName\": \"电源适配器\",\r\n  \"partNo\": \"3100-0123\",\r\n  \"qty\": 50,\r\n  \"batchNo\": \"20261008-A\",\r\n  \"prodDate\": \"2026-10-08\",\r\n  \"boxNo\": \"C2610080001\"\r\n}";

        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(settings, "服务地址", baseAddress);
        AddRow(settings, "API Key", apiKey);
        AddRow(settings, "模板编码", templateCode);
        AddRow(settings, "格式", format);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.Add(loadSchema);
        buttons.Controls.Add(render);
        settings.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        settings.Controls.Add(buttons, 1, settings.RowCount++);
        settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        settings.Controls.Add(new Label { Text = "数据", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, settings.RowCount);
        settings.Controls.Add(data, 1, settings.RowCount++);

        var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 420 };
        right.Panel1.Controls.Add(preview);
        right.Panel2.Controls.Add(log);
        var main = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 400 };
        main.Panel1.Controls.Add(settings);
        main.Panel2.Controls.Add(right);
        Controls.Add(main);

        loadSchema.Click += async (sender, e) => await LoadSchemaAsync();
        render.Click += async (sender, e) => await RenderAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            client?.Dispose();
            preview.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, table.RowCount);
        table.Controls.Add(control, 1, table.RowCount++);
    }

    private LabelServiceClient Client()
    {
        // 真实应用里客户端应全局单例；演示程序在连接设置变化时重建。
        var settings = baseAddress.Text + "|" + apiKey.Text;
        if (client == null || settings != clientSettings)
        {
            client?.Dispose();
            client = new LabelServiceClient(new LabelServiceClientOptions
            {
                BaseAddress = new Uri(baseAddress.Text.Trim()),
                ApiKey = apiKey.Text.Trim(),
            });
            client.CallCompleted += (sender, e) => Log(
                $"{e.Operation} {(e.ErrorCode ?? "OK")} 状态={e.StatusCode} 总耗时={e.Duration.TotalMilliseconds:0} ms 服务端={e.ServerDuration?.TotalMilliseconds:0.0} ms 尝试={e.Attempts} requestId={e.RequestId}");
            clientSettings = settings;
        }

        return client;
    }

    private async Task LoadSchemaAsync()
    {
        try
        {
            var schema = await Client().GetSchemaAsync(templateCode.Text);
            data.Text = JsonSerializer.Serialize(schema.Sample, PrettyJson).Replace("\n", "\r\n");
            Log($"读取字段：{schema.Name} v{schema.Version}，{schema.Fields.Count} 个字段，{schema.Page.Width}×{schema.Page.Height} mm");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task RenderAsync()
    {
        try
        {
            var labelFormat = (LabelFormat)Enum.Parse(typeof(LabelFormat), (string)format.SelectedItem!, ignoreCase: true);
            var request = new RenderRequest(templateCode.Text.Trim(), labelFormat);
            using (var document = JsonDocument.Parse(data.Text))
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    request.Set(property.Name, ToValue(property.Value));
                }
            }

            var file = await Client().RenderAsync(request);
            Log($"生成成功：{file.FileName}，v{file.TemplateVersion}，{file.LabelCount} 张，{file.Dpi} dpi，{file.Content.Length:N0} 字节，Server-Timing {string.Join(" ", file.ServerTiming.Select(p => p.Key + "=" + p.Value.ToString("0.0", CultureInfo.InvariantCulture)))}");
            if (file.Format == LabelFormat.Png)
            {
                preview.Image?.Dispose();
                using (var stream = file.OpenRead())
                {
                    preview.Image = new Bitmap(stream);
                }
            }
            else
            {
                var path = Path.Combine(Path.GetTempPath(), file.FileName);
                await file.SaveAsync(path);
                Log("已保存到 " + path);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static object? ToValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString();
            case JsonValueKind.Number:
                return value.GetDecimal();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            default:
                return value.GetRawText();
        }
    }

    private void ShowError(Exception ex)
    {
        if (ex is LabelServiceException service)
        {
            Log($"失败：{service.ErrorCode} {service.Message} traceId={service.TraceId}");
            foreach (var error in service.Errors)
            {
                Log($"  - {error.Field} {error.Code} {error.Message}");
            }
        }
        else
        {
            Log("失败：" + ex.Message);
        }
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Log(message)));
            return;
        }

        log.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {message}\r\n");
    }
}
