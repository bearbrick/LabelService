using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using LabelService.Client;
using LabelService.Client.Printing;

namespace LabelService.Demo;

/// <summary>
/// 自检（开发用）：把主要流程走一遍，每一步截一张窗口图，结果写进 smoke.txt，然后退出（全部正常时退出码 0）。不影响正常使用。
/// </summary>
/// <remarks>
/// <c>LabelServiceDemo.exe --smoke 输出目录</c> 用内置模拟服务（含 503 抖动）；
/// <c>LabelServiceDemo.exe --smoke 输出目录 http://localhost:5080/</c> 连真实服务，三种格式都生成一遍。
/// </remarks>
internal sealed partial class MainForm
{
    /// <summary>
    /// 调用代码必须使用统一静默入口，并给出特殊参数及网络 ZPL 的写法。
    /// </summary>
    private static readonly string[] PrintExamples =
    [
        "document.PrinterSettings.Copies = 2;",
        "document.Print();",
        "await NetworkPrinter.SendAsync(\"192.168.1.50\", NetworkPrinter.DefaultPort, file.Content);",
        "Windows 8.1+",
        "ex is COMException",
    ];

    /// <summary>
    /// 自检的输出目录；不是自检时为 null。
    /// </summary>
    private string? smokeDirectory;

    /// <summary>
    /// 自检连的真实服务地址；null 表示用内置模拟服务。
    /// </summary>
    private string? smokeServer;

    /// <summary>
    /// 窗口显示后跑自检，结果写到 <paramref name="directory"/>；<paramref name="server"/> 为空时用内置模拟服务。
    /// </summary>
    /// <param name="directory">输出目录（截图和 smoke.txt）。</param>
    /// <param name="server">真实服务的地址；null 表示用内置模拟服务。</param>
    public void RunSmokeOnShown(string directory, string? server)
    {
        smokeDirectory = directory;
        smokeServer = server;
    }

    /// <summary>
    /// 按顺序走一遍：界面缩放、刷新模板、读取字段、三种格式生成、批量数据和粘贴、四种错误、慢调用、调用代码；每一步记 PASS/FAIL，关键步骤截图，最后关闭窗口。
    /// </summary>
    /// <param name="directory">输出目录。</param>
    /// <param name="server">真实服务的地址；null 表示用内置模拟服务。</param>
    /// <returns>自检结束（窗口已关闭）时完成。</returns>
    private async Task RunSmokeAsync(string directory, string? server)
    {
        Directory.CreateDirectory(directory);
        var report = new StringBuilder();
        var reportPath = Path.Combine(directory, "smoke.txt");
        var failures = 0;
        void Check(string name, bool ok)
        {
            Application.DoEvents();
            report.AppendLine((ok ? "PASS " : "FAIL ") + name);
            failures += ok ? 0 : 1;

            // 每步都写一次：卡住时能看出停在哪一步
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        }

        void Shot(string name)
        {
            Application.DoEvents();
            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            bitmap.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
        }

        report.AppendLine(server is null ? "内置模拟服务" : "真实服务 " + server);
        useMock.Checked = server is null;
        flaky.Checked = server is null;
        baseAddress.Text = server ?? baseAddress.Text;
        Check("窗口使用内嵌条码图标，不依赖运行目录里的外部图标文件", ReferenceEquals(Icon, DemoIcon.Value) && typeof(MainForm).Assembly.GetManifestResourceNames().Contains("LabelService.Demo.AppIcon.ico"));
        Check($"界面缩放（{DeviceDpi} dpi）：左栏的文字都不折行，DPI 下拉框放得下“模板默认”", LeftLabelsFit() && dpi.Width >= TextRenderer.MeasureText("模板默认", dpi.Font).Width + SystemInformation.VerticalScrollBarWidth);
        Check("主窗口的横排下拉框文字和边框显示完整", DropdownRowsFit(this));

        await RefreshTemplatesAsync();
        Check("刷新模板：下拉框里有这把 Key 能用的模板，带名称", templateCode.Items.Contains("BOX_LABEL") && templateCode.Items.Contains("A4_SHIPPING_MARK") && templateList["BOX_LABEL"].Name.Length > 0);
        await ReadSchemaAsync();
        var required = fields.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => row.Cells["required"].Value is true);
        Check("读取字段：表格里有 BOX_LABEL 的全部字段，样例值已填好", schema is not null && fields.Rows.Count == schema.Fields.Count && required is not null && Cell(required, "value").Length > 0);
        Check("批量数据：列是模板的全部字段，默认 3 行，按钮写着条数", schema is not null && batchRows.Columns.Count == schema.Fields.Count && batchRows.Rows.Count == 3 && renderBatch.Text == "批量生成 3 条");
        Check("状态栏显示当前连接的服务", endpoint.Text == (server ?? "内置模拟服务"));

        png.Checked = true;
        await RenderSingleAsync();
        Check("生成单张 PNG，有预览图", result is { Format: LabelFormat.Png, LabelCount: 1 } && images.Count == 1);
        if (result is not null)
        {
            // 只显示打印窗口核对布局，不触发发送按钮或提交打印任务。
            using var printWindow = new PrintForm(result);
            printWindow.Show(this);
            Application.DoEvents();
            Check("选择其他打印机窗口的下拉框未被父容器裁切", DropdownRowsFit(printWindow));
            using var printBitmap = new Bitmap(printWindow.Width, printWindow.Height);
            printWindow.DrawToBitmap(printBitmap, new Rectangle(0, 0, printWindow.Width, printWindow.Height));
            printBitmap.Save(Path.Combine(directory, "0-print-layout.png"), ImageFormat.Png);
            printWindow.Close();
        }
        if (server is null)
        {
            // 调用日志是 CallCompleted 事件异步加进列表的（BeginInvoke）：等这次生成的那条加进来再看，最多等 1 秒
            for (var i = 0; i < 50 && (logList.Items.Count == 0 || logList.Items[0].SubItems[1].Text != "Render"); i++)
            {
                Application.DoEvents();
                await Task.Delay(20);
            }

            Check("模拟网络抖动：调用日志里第一条是重试 1 次后成功", logList.Items.Count > 0 && logList.Items[0].SubItems[6].Text == "1" && logList.Items[0].SubItems[9].Text.Contains("成功"));
        }

        Shot("1-single-png");

        pdf.Checked = true;
        await RenderSingleAsync();
        Check("生成单张 PDF，文件以 %PDF 开头，预览区提示用默认程序打开", result is { Format: LabelFormat.Pdf, LabelCount: 1 } && Encoding.ASCII.GetString(result.Content, 0, 4) == "%PDF" && images.Count == 0);

        zpl.Checked = true;
        SetBatchRowCount(5);
        await RenderBatchAsync();
        Check("批量 5 条 ZPL（每条数据给全），解出 5 张黑白预览", result is { Format: LabelFormat.Zpl, LabelCount: 5 } && images.Count == 5);
        Check("批量生成自动保留 5 个可打印项，选择第二项不会重新生成", batchResult is { Items.Count: 5 }
            && printItem.Items.Count == 5 && printSelected.Enabled && batchResult.Items[1].ItemNumber == 2);
        printItem.SelectedIndex = 1;
        Check("选择第二个生成项同步预览", current == 1);
        ShowImage(4);
        Check("点击第五张缩略图选择第五个补打项", printItem.SelectedIndex == 4);
        Shot("2-batch-zpl");

        pdf.Checked = true;
        await RenderBatchAsync();
        Check("批量 PDF 自动拆成 5 个业务文件，支持第二项图形文档", batchResult is { Items.Count: 5 }
            && batchResult.Items[1].Files[0].Format == LabelFormat.Pdf);
        using (var document = WindowsLabelPrinter.CreateDocument(batchResult!.Items[1]))
        {
            Check("PDF 单项打印文档创建成功，无需打开阅读器", document.PrintController is System.Drawing.Printing.StandardPrintController);
        }
        png.Checked = true;
        await RenderBatchAsync();
        Check("批量 PNG 保留 5 项并显示所选项工具栏", batchResult is { Items.Count: 5 } && printItem.Items.Count == 5
            && batchPrintTools is { Visible: true });
        Check("批量选择工具栏显示后，下拉框的文字和边框完整", DropdownRowsFit(this));
        Shot("2c-batch-selection");
        zpl.Checked = true;

        // 从 Excel 复制的两行，表头混用中文名称和 key：替换原有数据、按表头对列；没给的字段留空，服务端按条报缺字段
        PasteBatchText("箱号\tpartName\tqty\r\nC1\t电源适配器\t50\r\nC2\t电源适配器\t60\r\n");
        Check("粘贴带表头的两行：替换成 2 行，按表头对列（箱号 → boxNo）",
            batchRows.Rows.Count == 2 && BatchCell(1, "boxNo") == "C2" && BatchCell(1, "qty") == "60" && BatchCell(0, "batchNo") == "");
        await RenderBatchAsync();
        Check("批量数据缺必填字段：响应信息显示 FIELD_REQUIRED（按条列出）", result is null && ResponseHas(LabelErrorCodes.FieldRequired));
        Check("生成失败会清除旧集合和打印选择，避免补打旧内容", batchResult is null && printItem.Items.Count == 0 && !printSelected.Enabled);
        batchRows.CurrentCell = batchRows.Rows[0].Cells["boxNo"];
        PasteBatchText("C3\nC4\nC5");
        Check("粘贴不带表头的一列：从选中的格子往下填，行不够时加行", batchRows.Rows.Count == 3 && BatchCell(0, "boxNo") == "C3" && BatchCell(2, "boxNo") == "C5" && BatchCell(1, "partName") == "电源适配器");
        Shot("2b-batch-rows");

        // 设计书第 11 节验收：缺必填、Key 无效、模板未发布、批量超量时显示的错误码和第 8 节一致
        var value = required is null ? "" : Cell(required, "value");
        SetValue(required, "");
        await RenderSingleAsync();
        Check("缺必填字段：响应信息显示 FIELD_REQUIRED 和逐条错误", result is null && ResponseHas(LabelErrorCodes.FieldRequired));
        tabs.SelectedTab = responseTab;
        Shot("3-error-response");
        SetValue(required, value);

        var key = apiKey.Text;
        apiKey.Text = "wrong-key";
        await RenderSingleAsync();
        Check("Key 无效：响应信息显示 UNAUTHORIZED", ResponseHas(LabelErrorCodes.Unauthorized));
        apiKey.Text = key;

        templateCode.Text = "NOT_PUBLISHED";
        await RenderSingleAsync();
        Check("模板未发布：响应信息显示 TEMPLATE_NOT_FOUND", ResponseHas(LabelErrorCodes.TemplateNotFound));
        templateCode.Text = "BOX_LABEL";
        await ReadSchemaAsync();

        SetBatchRowCount(600);
        await RenderBatchAsync();
        Check("批量超量：SDK 本地拦下，响应信息对照服务端错误码 BATCH_TOO_LARGE", result is null && ResponseHas(LabelErrorCodes.BatchTooLarge));
        Shot("4-batch-too-large");

        // 慢调用：单张服务端超过 100 ms 的标出来。A4 唛头图大，模拟服务画一张就超过 100 ms
        templateCode.Text = "A4_SHIPPING_MARK";
        png.Checked = true;
        await RenderSingleAsync();
        Application.DoEvents();
        var serverMs = result?.ServerDuration?.TotalMilliseconds ?? 0;
        var marked = logList.Items.Count > 0 && logList.Items[0].SubItems[9].Text.StartsWith("慢调用", StringComparison.Ordinal);
        Check($"慢调用：A4 单张服务端 {serverMs:0} ms，超过 100 ms 的才标为慢调用", result is { LabelCount: 1 } && marked == serverMs > 100 && (server is not null || marked));
        templateCode.Text = "BOX_LABEL";
        zpl.Checked = true;
        await ReadSchemaAsync();

        tabs.SelectedTab = logTab;
        Shot("5-log");

        // 两种写法的代码都留一份，验收时放进新建的 net48 / net10 控制台项目编译
        File.WriteAllText(Path.Combine(directory, "Program.Single.cs.txt"), codeText.Text, Encoding.UTF8);
        codeBatch.Checked = true;
        tabs.SelectedTab = codeTab;
        Check("调用代码：批量写法每条用 AddItem().Set 给全数据，没有公共字段和递增",
            codeText.Text.Contains("new BatchRenderRequest(\"BOX_LABEL\", LabelFormat.Zpl)") && codeText.Text.Contains("batch.AddItem()") && codeText.Text.Contains(".Set(\"boxNo\"")
            && !codeText.Text.Contains("SetCommon") && !codeText.Text.Contains("ToString(\"D"));
        File.WriteAllText(Path.Combine(directory, "Program.Batch.cs.txt"), codeText.Text, Encoding.UTF8);
        Check("调用代码：直接用内存里的结果打印，不先存文件（另存一份写成注释）",
            !codeText.Text.Split('\n').Any(line => line.TrimStart().StartsWith("file.Save(", StringComparison.Ordinal))
            && codeText.Text.Contains("// file.Save(Path.Combine(Path.GetTempPath(), file.FileName));"));
        Shot("6-code");

        // 三种格式、单张和批量的代码各留一份，核对统一静默入口和特殊参数说明。
        var incomplete = new List<string>();
        foreach (var radio in new[] { png, pdf, zpl })
        {
            radio.Checked = true;
            foreach (var batchCode in new[] { false, true })
            {
                codeBatch.Checked = batchCode;
                var name = $"{radio.Text}.{(batchCode ? "Batch" : "Single")}";
                File.WriteAllText(Path.Combine(directory, $"Program.{name}.cs.txt"), codeText.Text, Encoding.UTF8);

                // ZPL 提示 DPI 要和打印机一致，PDF、PNG 提示可以拼版
                var hint = radio == zpl ? ".Dpi = " : ".Layout = \"A4_100x60_2x4\";";
                var printSource = batchCode ? "result.Items" : "file";
                if (!PrintExamples.All(codeText.Text.Contains) || !codeText.Text.Contains(hint)
                    || !codeText.Text.Contains("WindowsLabelPrinter.Print(" + printSource + ", PrinterName);")
                    || !codeText.Text.Contains("WindowsLabelPrinter.CreateDocument(" + printSource + ", PrinterName)")
                    || (batchCode && (!codeText.Text.Contains("RenderBatchFilesAsync(batch)") || !codeText.Text.Contains("Print(result.Items[1], PrinterName)")))
                    || codeText.Text.Contains("Process.Start") || codeText.Text.Contains("Verb ="))
                {
                    incomplete.Add(name);
                }
            }
        }

        Check("调用代码：三种格式、单张和批量都使用统一静默打印入口，不调用阅读器"
            + (incomplete.Count > 0 ? "，不全的：" + string.Join("、", incomplete) : ""), incomplete.Count == 0);

        zpl.Checked = true;
        codeBatch.Checked = false;

        report.AppendLine(failures == 0 ? "自检通过" : $"自检不通过：{failures} 项");
        File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        Environment.ExitCode = failures == 0 ? 0 : 1;
        Close();
    }

    /// <summary>
    /// 核对所有横排下拉框的实际边界，确保文字和边框不会被父容器裁切。
    /// </summary>
    /// <param name="control">窗口或控件子树的根节点。</param>
    /// <returns>全部横排下拉框显示完整时为 true。</returns>
    private static bool DropdownRowsFit(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child is ComboBox combo && combo.Parent is FlowLayoutPanel flow
                && (combo.Top < 0 || combo.Bottom > flow.ClientSize.Height))
            {
                return false;
            }

            if (!DropdownRowsFit(child))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 改字段表格里一行的值并刷新必填标记。
    /// </summary>
    /// <param name="row">行；null 时什么也不做。</param>
    /// <param name="value">新值。</param>
    private void SetValue(DataGridViewRow? row, string value)
    {
        if (row is not null)
        {
            row.Cells["value"].Value = value;
            MarkRequired();
        }
    }

    /// <summary>
    /// 批量数据某一格的文字。
    /// </summary>
    /// <param name="row">第几行，从 0 开始。</param>
    /// <param name="key">字段 key（列名）。</param>
    /// <returns>文字；空格子为空字符串。</returns>
    private string BatchCell(int row, string key) => batchRows.Rows[row].Cells[key].Value as string ?? "";

    /// <summary>
    /// 左栏里自动大小的文字都没有折行（高度不到一行半）。
    /// </summary>
    /// <returns>都没折行时为 true。</returns>
    private bool LeftLabelsFit()
    {
        Control left = dataTabs;
        while (left.Parent is { } parent && parent is not SplitterPanel)
        {
            left = parent;
        }

        return Descendants(left).OfType<Label>().Where(label => label.AutoSize && label.Text.Length > 0)
            .All(label => label.Height < TextRenderer.MeasureText(label.Text, label.Font).Height * 3 / 2);
    }

    /// <summary>
    /// 响应信息里有没有显示某个错误码。
    /// </summary>
    /// <param name="errorCode">错误码。</param>
    /// <returns>有时为 true。</returns>
    private bool ResponseHas(string errorCode) =>
        responseList.Items.Cast<ListViewItem>().Any(item => item.SubItems[1].Text == errorCode);

    /// <inheritdoc />
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (smokeDirectory is { } directory)
        {
            try
            {
                await RunSmokeAsync(directory, smokeServer);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "smoke.txt"), "自检出错：" + ex, Encoding.UTF8);
                Environment.ExitCode = 2;
                Close();
            }
        }
        else
        {
            await LoadTemplatesOnStartAsync();
        }
    }
}
