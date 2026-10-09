using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using LabelService.Client;

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
    private string? smokeDirectory;
    private string? smokeServer;

    /// <summary>窗口显示后跑自检，结果写到 <paramref name="directory"/>；<paramref name="server"/> 为空时用内置模拟服务。</summary>
    public void RunSmokeOnShown(string directory, string? server)
    {
        smokeDirectory = directory;
        smokeServer = server;
    }

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
        await ReadSchemaAsync();
        var required = fields.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => row.Cells["required"].Value is true);
        Check("读取字段：表格里有 BOX_LABEL 的全部字段，样例值已填好", schema is not null && fields.Rows.Count == schema.Fields.Count && required is not null && Cell(required, "value").Length > 0);
        Check("状态栏显示当前连接的服务", endpoint.Text == (server ?? "内置模拟服务"));

        png.Checked = true;
        await RenderSingleAsync();
        Check("生成单张 PNG，有预览图", result is { Format: LabelFormat.Png, LabelCount: 1 } && images.Count == 1);
        if (server is null)
        {
            Check("模拟网络抖动：调用日志里第一条是重试 1 次后成功", logList.Items.Count > 0 && logList.Items[0].SubItems[6].Text == "1" && logList.Items[0].SubItems[9].Text.Contains("成功"));
        }

        Shot("1-single-png");

        pdf.Checked = true;
        await RenderSingleAsync();
        Check("生成单张 PDF，文件以 %PDF 开头，预览区提示用默认程序打开", result is { Format: LabelFormat.Pdf, LabelCount: 1 } && Encoding.ASCII.GetString(result.Content, 0, 4) == "%PDF" && images.Count == 0);

        zpl.Checked = true;
        batchCount.Value = 5;
        await RenderBatchAsync();
        Check("批量 5 条 ZPL，解出 5 张黑白预览", result is { Format: LabelFormat.Zpl, LabelCount: 5 } && images.Count == 5);
        ShowImage(4);
        Shot("2-batch-zpl");

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

        batchCount.Value = 600;
        await RenderBatchAsync();
        Check("批量超量：SDK 本地拦下，响应信息对照服务端错误码 BATCH_TOO_LARGE", result is null && ResponseHas(LabelErrorCodes.BatchTooLarge));
        Shot("4-batch-too-large");
        batchCount.Value = 5;

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
        Check("调用代码：批量写法里有 BatchRenderRequest 和递增的箱号", codeText.Text.Contains("new BatchRenderRequest(\"BOX_LABEL\", LabelFormat.Zpl)") && codeText.Text.Contains("ToString(\"D"));
        File.WriteAllText(Path.Combine(directory, "Program.Batch.cs.txt"), codeText.Text, Encoding.UTF8);
        Shot("6-code");

        report.AppendLine(failures == 0 ? "自检通过" : $"自检不通过：{failures} 项");
        File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        Environment.ExitCode = failures == 0 ? 0 : 1;
        Close();
    }

    private void SetValue(DataGridViewRow? row, string value)
    {
        if (row is not null)
        {
            row.Cells["value"].Value = value;
            MarkRequired();
        }
    }

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
    }
}
