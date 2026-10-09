using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using LabelService.Client;
using LabelService.Demo.Mock;

namespace LabelService.Demo;

/// <summary>
/// 主窗口里调用 SDK 的部分：建客户端、测试连接、读取字段、生成单张和批量、记调用日志。
/// </summary>
internal sealed partial class MainForm
{
    private readonly MockLabelServiceHandler mock = new MockLabelServiceHandler();
    private LabelServiceClient? client;
    private string clientKey = "";
    private TemplateSchema? schema;
    private bool busy;

    private void WireEvents()
    {
        showKey.Click += (_, _) =>
        {
            apiKey.UseSystemPasswordChar = !apiKey.UseSystemPasswordChar;
            showKey.Text = apiKey.UseSystemPasswordChar ? "显示" : "隐藏";
        };
        useMock.CheckedChanged += (_, _) =>
        {
            baseAddress.Enabled = !useMock.Checked;
            UpdateEndpoint();
            UpdateCode();
        };
        baseAddress.TextChanged += (_, _) => UpdateEndpoint();
        flaky.CheckedChanged += (_, _) => mock.Flaky = flaky.Checked;
        testConnection.Click += async (_, _) => await TestConnectionAsync();
        readSchema.Click += async (_, _) => await ReadSchemaAsync();
        templateCode.TextChanged += (_, _) => schema = null;
        renderSingle.Click += async (_, _) => await RenderSingleAsync();
        renderBatch.Click += async (_, _) => await RenderBatchAsync();
        batchCount.ValueChanged += (_, _) => renderBatch.Text = $"批量生成 {batchCount.Value} 条";
        codeBatch.CheckedChanged += (_, _) => UpdateCode();
        zoom.SelectedIndexChanged += (_, _) => LayoutPreview();
        foreach (var control in new Control[] { baseAddress, timeout, retries, templateCode, dpi, copies, batchCount, incrementKey, png, pdf, zpl })
        {
            control.TextChanged += (_, _) => UpdateCode();
        }

        foreach (var radio in new[] { png, pdf, zpl })
        {
            radio.CheckedChanged += (_, _) => UpdateCode();
        }

        fields.CellValueChanged += (_, e) =>
        {
            MarkRequired();
            UpdateCode();
        };
        fields.CellEndEdit += (_, _) => MarkRequired();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F5)
            {
                renderSingle.PerformClick();
            }
        };
    }

    /// <summary>
    /// 按当前设置取客户端：设置没变就复用同一个实例（SDK 要求应用内单例），变了才新建。
    /// </summary>
    private LabelServiceClient Client()
    {
        var key = $"{useMock.Checked}|{baseAddress.Text}|{apiKey.Text}|{timeout.Value}|{retries.Value}";
        if (client is not null && key == clientKey)
        {
            return client;
        }

        client?.Dispose();
        var options = new LabelServiceClientOptions
        {
            BaseAddress = new Uri(useMock.Checked ? "http://mock.local/" : baseAddress.Text.Trim()),
            ApiKey = apiKey.Text.Trim(),
            Timeout = TimeSpan.FromSeconds((double)timeout.Value),
            MaxRetries = (int)retries.Value,
        };
        client = useMock.Checked ? new LabelServiceClient(options, mock) : new LabelServiceClient(options);
        client.CallCompleted += (_, e) => BeginInvoke(new Action(() => AddLog(e)));
        clientKey = key;
        return client;
    }

    private async Task TestConnectionAsync()
    {
        await RunAsync("测试连接", async () =>
        {
            try
            {
                var found = await Client().GetSchemaAsync(TemplateCodeText);
                MessageBox.Show(this, $"连接正常，模板 {found.TemplateCode}（{found.Name}）v{found.Version} 可以调用。", "测试连接", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (LabelServiceException ex) when (ex.ErrorCode == LabelErrorCodes.TemplateNotFound)
            {
                MessageBox.Show(this, $"连接和 API Key 正常，但模板 {TemplateCodeText} 不存在或还没有发布。", "测试连接", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        });
    }

    private async Task ReadSchemaAsync()
    {
        await RunAsync("读取字段", async () =>
        {
            schema = await Client().GetSchemaAsync(TemplateCodeText);
            var old = fields.Rows.Cast<DataGridViewRow>().ToDictionary(row => Cell(row, "key"), row => Cell(row, "value"));
            fields.Rows.Clear();
            incrementKey.Items.Clear();
            foreach (var field in schema.Fields)
            {
                var value = old.TryGetValue(field.Key, out var typed) && typed.Length > 0
                    ? typed
                    : schema.Sample.TryGetValue(field.Key, out var sample) ? (sample.ValueKind == System.Text.Json.JsonValueKind.String ? sample.GetString() ?? "" : sample.GetRawText()) : "";
                fields.Rows.Add(field.Key, field.Name ?? "", field.Type, field.Required, value);
                incrementKey.Items.Add(field.Key);
            }

            incrementKey.SelectedItem = schema.Fields.Select(field => field.Key).FirstOrDefault(key => key.EndsWith("No", StringComparison.Ordinal) && key.Contains("box"))
                ?? schema.Fields.LastOrDefault(field => field.Type == "string")?.Key;
            schemaInfo.Text = $"{schema.Name} · v{schema.Version} · {schema.Page.Width}×{schema.Page.Height} mm · {schema.Page.Dpi} dpi · {schema.Fields.Count} 个字段";
            MarkRequired();
            UpdateCode();
        });
    }

    private async Task RenderSingleAsync()
    {
        if (!await EnsureSchemaAsync())
        {
            return;
        }

        await RunAsync("RenderAsync", async () =>
        {
            var request = new RenderRequest(TemplateCodeText, SelectedFormat) { Dpi = SelectedDpi, Copies = (int)copies.Value };
            foreach (var (key, type, value) in FieldValues())
            {
                request.Set(key, Convert(type, value));
            }

            ShowResult(await Client().RenderAsync(request));
        });
    }

    private async Task RenderBatchAsync()
    {
        if (!await EnsureSchemaAsync())
        {
            return;
        }

        await RunAsync("RenderBatchAsync", async () =>
        {
            var batch = new BatchRenderRequest(TemplateCodeText, SelectedFormat)
            {
                Dpi = SelectedDpi,
                Output = SelectedFormat == LabelFormat.Png ? BatchOutput.Zip : BatchOutput.Merge,
            };
            var increment = incrementKey.SelectedItem as string;
            var values = FieldValues();
            foreach (var (key, type, value) in values.Where(field => field.Key != increment))
            {
                batch.SetCommon(key, Convert(type, value));
            }

            var start = values.FirstOrDefault(field => field.Key == increment);
            for (var i = 0; i < (int)batchCount.Value; i++)
            {
                var item = batch.AddItem();
                if (start.Key is not null)
                {
                    item.Set(start.Key, Convert(start.Type, CodeGenerator.Increment(start.Value, i)));
                }
            }

            ShowResult(await Client().RenderBatchAsync(batch));
        });
    }

    /// <summary>
    /// 统一处理按钮状态、状态栏和错误：SDK 抛的 LabelServiceException 和本地校验的 ArgumentException 都显示到“响应信息”。
    /// </summary>
    private async Task RunAsync(string operation, Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        foreach (var button in new[] { testConnection, readSchema, renderSingle, renderBatch })
        {
            button.Enabled = false;
        }

        status.Text = operation + " 进行中…";
        var started = DateTime.Now;
        try
        {
            await action();
            status.Text = $"{operation} 完成，{(DateTime.Now - started).TotalMilliseconds:0} ms · {(useMock.Checked ? "内置模拟服务" : baseAddress.Text)}";
        }
        catch (LabelServiceException ex)
        {
            ShowError(ex);
            status.Text = $"{operation} 失败：{ex.ErrorCode}";
        }
        catch (ArgumentException ex)
        {
            ShowError(ex);
            status.Text = $"{operation} 没有发出：{PlainMessage(ex)}";
        }
        catch (UriFormatException ex)
        {
            MessageBox.Show(this, "服务地址格式不对：" + ex.Message, "连接设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            status.Text = "服务地址格式不对";
        }
        finally
        {
            busy = false;
            foreach (var button in new[] { testConnection, readSchema, renderSingle, renderBatch })
            {
                button.Enabled = true;
            }
        }
    }

    private async Task<bool> EnsureSchemaAsync()
    {
        if (schema is null || !string.Equals(schema.TemplateCode, TemplateCodeText, StringComparison.OrdinalIgnoreCase))
        {
            await ReadSchemaAsync();
        }

        return schema is not null;
    }

    private void AddLog(CallCompletedEventArgs e)
    {
        var slow = e.Operation == "Render" && e.ServerDuration?.TotalMilliseconds > 100;
        var result = e.StatusCode is null ? e.ErrorCode ?? "—" : $"{e.StatusCode} {(e.ErrorCode ?? "OK")}";
        var note = slow ? "慢调用（服务端超过 100 ms）" : e.Attempts > 1 ? $"重试 {e.Attempts - 1} 次后{(e.ErrorCode is null ? "成功" : "仍失败")}" : "";
        var item = new ListViewItem(new[]
        {
            DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            e.Operation,
            e.TemplateCode,
            result,
            $"{e.Duration.TotalMilliseconds:0} ms",
            e.ServerDuration is { } server ? $"{server.TotalMilliseconds:0.0} ms" : "—",
            (e.Attempts - 1).ToString(CultureInfo.InvariantCulture),
            e.RequestId,
            e.TraceId ?? "—",
            note,
        });
        if (slow)
        {
            item.BackColor = Color.FromArgb(0xFD, 0xF6, 0xE7);
        }
        else if (e.ErrorCode is not null)
        {
            item.ForeColor = Color.FromArgb(0xB8, 0x58, 0x62);
        }

        logList.Items.Insert(0, item);
        logTab.Text = $"调用日志 ({logList.Items.Count})";
    }

    private List<(string Key, string Type, string Value)> FieldValues()
    {
        fields.EndEdit();
        return fields.Rows.Cast<DataGridViewRow>()
            .Select(row => (Cell(row, "key"), Cell(row, "type"), Cell(row, "value").Trim()))
            .Where(field => field.Item3.Length > 0)
            .ToList();
    }

    /// <summary>
    /// 表格里的文本按字段类型转换：number 转数字，date 转 DateTime，image 当作文件路径读成字节（SDK 转 base64）。
    /// </summary>
    private static object Convert(string type, string value)
    {
        switch (type)
        {
            case "number" when decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number):
                return number;
            case "date" when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date):
                return date;
            case "image" when File.Exists(value):
                return File.ReadAllBytes(value);
            default:
                return value;
        }
    }

    /// <summary>必填字段的值为空时标红。</summary>
    private void MarkRequired()
    {
        foreach (DataGridViewRow row in fields.Rows)
        {
            var empty = row.Cells["required"].Value is true && string.IsNullOrWhiteSpace(Cell(row, "value"));
            row.Cells["value"].Style.BackColor = empty ? Color.FromArgb(0xFD, 0xF1, 0xF2) : SystemColors.Window;
        }
    }

    private void UpdateEndpoint() => endpoint.Text = useMock.Checked ? "内置模拟服务" : baseAddress.Text;

    private void UpdateCode()
    {
        var settings = new CodeGenerator.Settings
        {
            BaseAddress = useMock.Checked ? "http://localhost:5080/" : baseAddress.Text.Trim(),
            TimeoutSeconds = (int)timeout.Value,
            MaxRetries = (int)retries.Value,
            TemplateCode = TemplateCodeText,
            Format = SelectedFormat,
            Dpi = SelectedDpi,
            Copies = (int)copies.Value,
            Batch = codeBatch.Checked,
            BatchCount = (int)batchCount.Value,
            IncrementKey = incrementKey.SelectedItem as string,
        };
        settings.Fields.AddRange(fields.Rows.Cast<DataGridViewRow>().Select(row => (Cell(row, "key"), Cell(row, "type"), Cell(row, "value").Trim())));
        codeText.Text = CodeGenerator.Generate(settings).Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");

        // 显式设置一次选区，否则文本框第一次获得焦点时会全选
        codeText.Select(0, 0);
    }

    private static string Cell(DataGridViewRow row, string column) => row.Cells[column].Value as string ?? "";

    private string TemplateCodeText => templateCode.Text.Trim();

    private LabelFormat SelectedFormat => pdf.Checked ? LabelFormat.Pdf : zpl.Checked ? LabelFormat.Zpl : LabelFormat.Png;

    private int? SelectedDpi => dpi.SelectedIndex > 0 ? int.Parse((string)dpi.SelectedItem!, CultureInfo.InvariantCulture) : null;
}
