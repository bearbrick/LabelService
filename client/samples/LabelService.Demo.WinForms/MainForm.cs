using System.Drawing;
using System.Windows.Forms;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 演示程序主窗口（设计书第 11 节、原型 docs/prototypes/label-sdk-demo-prototype.html）：左边填参数，右边看结果。
/// 这个文件只摆界面；调用 SDK 在 MainForm.Calls.cs，显示结果在 MainForm.Results.cs。
/// </summary>
internal sealed partial class MainForm : Form
{
    private static readonly Font Mono = new Font("Consolas", 9.5f);

    // 连接设置
    private readonly TextBox baseAddress = new TextBox { Text = "http://localhost:5080/", Dock = DockStyle.Fill };
    private readonly TextBox apiKey = new TextBox { Text = "lbl_test_local_dev", UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly Button showKey = new Button { Text = "显示", AutoSize = true, Margin = new Padding(3, 0, 0, 0) };
    private readonly NumericUpDown timeout = new NumericUpDown { Minimum = 1, Maximum = 300, Value = 30, Width = 60 };
    private readonly NumericUpDown retries = new NumericUpDown { Minimum = 0, Maximum = 5, Value = 2, Width = 50 };
    private readonly CheckBox useMock = new CheckBox { Text = "使用内置模拟服务（不连服务器也能演示）", AutoSize = true };
    private readonly CheckBox flaky = new CheckBox { Text = "模拟网络抖动（首次返回 503，看自动重试）", AutoSize = true };
    private readonly Button testConnection = new Button { Text = "测试连接", AutoSize = true };

    // 模板与数据
    private readonly ComboBox templateCode = new ComboBox { Text = "BOX_LABEL", Dock = DockStyle.Fill };
    private readonly Button readSchema = new Button { Text = "读取字段", AutoSize = true, Margin = new Padding(3, 0, 0, 0) };
    private readonly DataGridView fields = new DataGridView
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersWidth = 24,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.FixedSingle,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        EditMode = DataGridViewEditMode.EditOnEnter,
    };
    private readonly Label schemaInfo = new Label { Text = "点“读取字段”从服务端取字段定义（GetSchemaAsync）", AutoSize = true, ForeColor = SystemColors.GrayText };

    // 生成选项
    private readonly RadioButton png = new RadioButton { Text = "PNG", AutoSize = true, Checked = true };
    private readonly RadioButton pdf = new RadioButton { Text = "PDF", AutoSize = true };
    private readonly RadioButton zpl = new RadioButton { Text = "ZPL", AutoSize = true };
    private readonly ComboBox dpi = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly NumericUpDown copies = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 1, Width = 60 };
    private readonly NumericUpDown batchCount = new NumericUpDown { Minimum = 1, Maximum = 600, Value = 10, Width = 60 };
    private readonly ComboBox incrementKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Button renderSingle = new Button { Text = "生成单张", AutoSize = true };
    private readonly Button renderBatch = new Button { Text = "批量生成 10 条", AutoSize = true };

    // 右边
    private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
    private readonly TabPage previewTab = new TabPage("预览");
    private readonly TabPage codeTab = new TabPage("调用代码");
    private readonly TabPage logTab = new TabPage("调用日志");
    private readonly TabPage responseTab = new TabPage("响应信息");
    private readonly Label resultInfo = new Label { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
    private readonly Panel previewHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(0xF3, 0xF5, 0xF8), AutoScroll = true };
    private readonly FlowLayoutPanel thumbnails = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 0, AutoScroll = true, BackColor = SystemColors.Control };
    private readonly ComboBox zoom = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly TextBox codeText = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = Mono, BackColor = SystemColors.Window };
    private readonly CheckBox codeBatch = new CheckBox { Text = "批量写法", AutoSize = true, Margin = new Padding(12, 6, 3, 3) };
    private readonly ListView logList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
    private readonly ListView responseList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };

    // 菜单和状态栏
    private readonly ToolStripMenuItem saveMenu = new ToolStripMenuItem("保存结果…") { ShortcutKeys = Keys.Control | Keys.S, Enabled = false };
    private readonly ToolStripMenuItem openMenu = new ToolStripMenuItem("用默认程序打开") { Enabled = false };
    private readonly ToolStripMenuItem printMenu = new ToolStripMenuItem("发送到打印机…") { ShortcutKeys = Keys.Control | Keys.P, Enabled = false };
    private readonly ToolStripStatusLabel status = new ToolStripStatusLabel("就绪") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel endpoint = new ToolStripStatusLabel();
    private readonly List<Button> resultButtons = new List<Button>();

    public MainForm()
    {
        Text = "标签服务 SDK 调用演示 - LabelServiceDemo";
        Font = new Font("Microsoft YaHei UI", 9f);
        ClientSize = new Size(1240, 760);
        MinimumSize = new Size(960, 600);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        dpi.Items.AddRange(new object[] { "模板默认", "203", "300", "600" });
        dpi.SelectedIndex = 0;
        zoom.Items.AddRange(new object[] { "适应窗口", "100%", "50%" });
        zoom.SelectedIndex = 0;
        templateCode.Items.AddRange(new object[] { "BOX_LABEL", "A4_SHIPPING_MARK" });

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = 6 };
        split.Panel1.Controls.Add(BuildLeft());
        split.Panel2.Controls.Add(BuildRight());
        Controls.Add(split);
        Controls.Add(BuildHeader());
        Controls.Add(BuildMenu());
        Controls.Add(BuildStatus());
        Load += (_, _) => split.SplitterDistance = 440;

        WireEvents();
        UpdateEndpoint();
        UpdateCode();
        ShowResult(null);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 44, ColumnCount = 3, Padding = new Padding(10, 6, 10, 6), BackColor = Color.FromArgb(0x07, 0x1F, 0x46) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new Label { Text = "标签服务 SDK 调用演示", AutoSize = true, ForeColor = Color.White, Font = new Font(Font.FontFamily, 11f, FontStyle.Bold), Anchor = AnchorStyles.Left });
        header.Controls.Add(new Label { Text = "读取模板字段、填数据、生成标签、发送到打印机", AutoSize = true, ForeColor = Color.FromArgb(0xB8, 0xC7, 0xE0), Anchor = AnchorStyles.Left });
        header.Controls.Add(new Label { Text = $"LabelService.Client {SdkVersion} · {RuntimeName}", AutoSize = true, ForeColor = Color.FromArgb(0xB8, 0xC7, 0xE0), Anchor = AnchorStyles.Right });
        return header;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var exit = new ToolStripMenuItem("退出(&X)", null, (_, _) => Close()) { ShortcutKeys = Keys.Alt | Keys.F4 };
        menu.Items.Add(new ToolStripMenuItem("文件(&F)", null, saveMenu, openMenu, new ToolStripSeparator(), exit));
        var clearLog = new ToolStripMenuItem("清空调用日志", null, (_, _) => logList.Items.Clear());
        menu.Items.Add(new ToolStripMenuItem("工具(&T)", null, new ToolStripMenuItem("测试连接", null, async (_, _) => await TestConnectionAsync()), printMenu, new ToolStripSeparator(), clearLog));
        var showCode = new ToolStripMenuItem("查看调用代码", null, (_, _) => tabs.SelectedTab = codeTab);
        var about = new ToolStripMenuItem("关于 LabelServiceDemo", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripMenuItem("帮助(&H)", null, showCode, about));
        saveMenu.Click += (_, _) => SaveResult();
        openMenu.Click += (_, _) => OpenResult();
        printMenu.Click += (_, _) => PrintResult();
        return menu;
    }

    private StatusStrip BuildStatus()
    {
        var strip = new StatusStrip();
        strip.Items.Add(status);
        strip.Items.Add(endpoint);
        strip.Items.Add(new ToolStripStatusLabel("SDK " + SdkVersion) { BorderSides = ToolStripStatusLabelBorderSides.Left });
        strip.Items.Add(new ToolStripStatusLabel(RuntimeName) { BorderSides = ToolStripStatusLabelBorderSides.Left });
        return strip;
    }

    private Control BuildLeft()
    {
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var connection = Grid();
        AddRow(connection, "服务地址", baseAddress);
        AddRow(connection, "API Key", Line(apiKey, showKey));
        AddRow(connection, "超时（秒）", Flow(timeout, new Label { Text = "重试次数", AutoSize = true, Margin = new Padding(12, 6, 3, 0) }, retries));
        AddRow(connection, "", useMock);
        AddRow(connection, "", flaky);
        AddRow(connection, "", testConnection);
        left.Controls.Add(Group("连接设置", connection), 0, 0);

        var data = Grid();
        AddRow(data, "模板编码", Line(templateCode, readSchema));
        data.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        data.Controls.Add(fields, 0, data.RowCount);
        data.SetColumnSpan(fields, 2);
        data.RowCount++;
        data.Controls.Add(schemaInfo, 0, data.RowCount);
        data.SetColumnSpan(schemaInfo, 2);
        data.RowCount++;
        var dataGroup = Group("模板与数据", data);
        dataGroup.Dock = DockStyle.Fill;
        left.Controls.Add(dataGroup, 0, 1);
        fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "key", HeaderText = "字段", ReadOnly = true, FillWeight = 26 });
        fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "名称", ReadOnly = true, FillWeight = 22 });
        fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "类型", ReadOnly = true, FillWeight = 16 });
        fields.Columns.Add(new DataGridViewCheckBoxColumn { Name = "required", HeaderText = "必填", ReadOnly = true, FillWeight = 12 });
        fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "value", HeaderText = "值", FillWeight = 44 });

        var options = Grid();
        AddRow(options, "格式", Flow(png, pdf, zpl, new Label { Text = "DPI", AutoSize = true, Margin = new Padding(12, 6, 3, 0) }, dpi));
        AddRow(options, "份数", Flow(copies, new Label { Text = "单张生成时每张打几份（1–100）", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(6, 6, 3, 0) }));
        AddRow(options, "批量条数", Flow(batchCount, new Label { Text = "递增字段", AutoSize = true, Margin = new Padding(12, 6, 3, 0) }, incrementKey));
        AddRow(options, "", Flow(renderSingle, renderBatch));
        left.Controls.Add(Group("生成选项", options), 0, 2);
        return left;
    }

    private Control BuildRight()
    {
        var previewTools = Toolbar(
            ResultButton("保存文件…", (_, _) => SaveResult()),
            ResultButton("用默认程序打开", (_, _) => OpenResult()),
            ResultButton("发送到打印机…", (_, _) => PrintResult()),
            new Label { Text = "缩放", AutoSize = true, Margin = new Padding(16, 7, 3, 0) },
            zoom);
        previewTab.Controls.Add(previewHost);
        previewTab.Controls.Add(thumbnails);
        previewTab.Controls.Add(resultInfo);
        previewTab.Controls.Add(previewTools);

        var copy = new Button { Text = "复制代码", AutoSize = true };
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(codeText.Text);
            status.Text = "已复制调用代码";
        };
        codeTab.Controls.Add(codeText);
        codeTab.Controls.Add(Toolbar(copy, codeBatch, Hint("根据左侧当前设置生成，C# 7.3 起可用（.NET Framework 4.6.2+ / .NET 8+）")));

        foreach (var (text, width) in new[] { ("时间", 70), ("操作", 90), ("模板", 120), ("结果", 140), ("总耗时", 70), ("服务端", 70), ("重试", 44), ("requestId", 90), ("traceId", 120), ("说明", 200) })
        {
            logList.Columns.Add(text, width);
        }

        var clear = new Button { Text = "清空", AutoSize = true };
        clear.Click += (_, _) => logList.Items.Clear();
        logTab.Controls.Add(logList);
        logTab.Controls.Add(Toolbar(clear, Hint("来自 LabelServiceClient.CallCompleted 事件；单张服务端超过 100 ms 标为慢调用")));

        responseList.Columns.Add("属性", 180);
        responseList.Columns.Add("值", 560);
        responseTab.Controls.Add(responseList);

        tabs.TabPages.AddRange(new[] { previewTab, codeTab, logTab, responseTab });
        return tabs;
    }

    private Button ResultButton(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Enabled = false };
        button.Click += click;
        resultButtons.Add(button);
        return button;
    }

    private static TableLayoutPanel Grid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, grid.RowCount);
        grid.Controls.Add(control, 1, grid.RowCount);
        grid.RowCount++;
    }

    private static GroupBox Group(string text, Control content)
    {
        var group = new GroupBox { Text = text, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 4, 6, 6) };
        group.Controls.Add(content);
        return group;
    }

    private static Control Line(Control fill, Control right)
    {
        var line = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        line.Controls.Add(fill, 0, 0);
        line.Controls.Add(right, 1, 0);
        return line;
    }

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
        flow.Controls.AddRange(controls);
        return flow;
    }

    private static FlowLayoutPanel Toolbar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(4, 3, 4, 0), WrapContents = false };
        bar.Controls.AddRange(controls);
        return bar;
    }

    private static Label Hint(string text) =>
        new Label { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(12, 7, 3, 0) };

    private static string SdkVersion
    {
        get
        {
            var attribute = typeof(LabelServiceClient).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault();
            return attribute?.InformationalVersion.Split('+')[0] ?? typeof(LabelServiceClient).Assembly.GetName().Version?.ToString() ?? "";
        }
    }

    private static string RuntimeName =>
#if NETFRAMEWORK
        ".NET Framework 4.8";
#else
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
#endif
}
