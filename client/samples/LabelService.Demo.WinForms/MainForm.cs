using System.Drawing;
using System.Windows.Forms;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 演示程序主窗口（设计书第 11 节、原型 docs/prototypes/label-sdk-demo-prototype.html）：左边填参数，右边看结果。
/// 这个文件放控件和构造函数；界面布局在 MainForm.Layout.cs，调用 SDK 在 MainForm.Calls.cs，显示结果在 MainForm.Results.cs，自检在 MainForm.Smoke.cs。
/// </summary>
internal sealed partial class MainForm : Form
{
    /// <summary>
    /// 等宽字体，调用代码用。
    /// </summary>
    private static readonly Font Mono = new Font("Consolas", 9.5f);

    /// <summary>
    /// 连接设置：服务地址。
    /// </summary>
    private readonly TextBox baseAddress = new TextBox { Text = "http://localhost:5080/", Dock = DockStyle.Fill };

    /// <summary>
    /// 连接设置：API Key（默认是开发环境的测试 Key，按密码显示）。
    /// </summary>
    private readonly TextBox apiKey = new TextBox { Text = "lbl_test_local_dev", UseSystemPasswordChar = true, Dock = DockStyle.Fill };

    /// <summary>
    /// 连接设置：切换 API Key 明文显示。
    /// </summary>
    private readonly Button showKey = new Button { Text = "显示", AutoSize = true, Margin = new Padding(3, 0, 0, 0) };

    /// <summary>
    /// 连接设置：单次请求超时，秒。
    /// </summary>
    private readonly NumericUpDown timeout = new NumericUpDown { Minimum = 1, Maximum = 300, Value = 30, Width = 60 };

    /// <summary>
    /// 连接设置：自动重试次数。
    /// </summary>
    private readonly NumericUpDown retries = new NumericUpDown { Minimum = 0, Maximum = 5, Value = 2, Width = 50 };

    /// <summary>
    /// 连接设置：用内置模拟服务代替真实服务。
    /// </summary>
    private readonly CheckBox useMock = new CheckBox { Text = "使用内置模拟服务（不连服务器也能演示）", AutoSize = true };

    /// <summary>
    /// 连接设置：让模拟服务首次返回 503，演示自动重试。
    /// </summary>
    private readonly CheckBox flaky = new CheckBox { Text = "模拟网络抖动（首次返回 503，看自动重试）", AutoSize = true };

    /// <summary>
    /// 连接设置：测试连接按钮。
    /// </summary>
    private readonly Button testConnection = new Button { Text = "测试连接", AutoSize = true };

    /// <summary>
    /// 模板与数据：模板编码（可以选示例模板，也可以手填）。
    /// </summary>
    private readonly ComboBox templateCode = new ComboBox { Text = "BOX_LABEL", Dock = DockStyle.Fill };

    /// <summary>
    /// 模板与数据：读取字段按钮。
    /// </summary>
    private readonly Button readSchema = new Button { Text = "读取字段", AutoSize = true, Margin = new Padding(3, 0, 0, 0) };

    /// <summary>
    /// 模板与数据：字段表格，列是字段、名称、类型、必填、值，只有值可以编辑。
    /// </summary>
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

    /// <summary>
    /// 模板与数据：模板名称、版本、尺寸、字段个数。
    /// </summary>
    private readonly Label schemaInfo = new Label { Text = "点“读取字段”从服务端取字段定义（GetSchemaAsync）", AutoSize = true, ForeColor = SystemColors.GrayText };

    /// <summary>
    /// 生成选项：输出 PNG。
    /// </summary>
    private readonly RadioButton png = new RadioButton { Text = "PNG", AutoSize = true, Checked = true };

    /// <summary>
    /// 生成选项：输出 PDF。
    /// </summary>
    private readonly RadioButton pdf = new RadioButton { Text = "PDF", AutoSize = true };

    /// <summary>
    /// 生成选项：输出 ZPL。
    /// </summary>
    private readonly RadioButton zpl = new RadioButton { Text = "ZPL", AutoSize = true };

    /// <summary>
    /// 生成选项：分辨率，第一项是模板默认。
    /// </summary>
    private readonly ComboBox dpi = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };

    /// <summary>
    /// 生成选项：单张生成时的份数。
    /// </summary>
    private readonly NumericUpDown copies = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 1, Width = 60 };

    /// <summary>
    /// 生成选项：批量生成的条数。
    /// </summary>
    private readonly NumericUpDown batchCount = new NumericUpDown { Minimum = 1, Maximum = 600, Value = 10, Width = 60 };

    /// <summary>
    /// 生成选项：批量时每条递增的字段（如箱号）。
    /// </summary>
    private readonly ComboBox incrementKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };

    /// <summary>
    /// 生成选项：生成单张按钮（快捷键 F5）。
    /// </summary>
    private readonly Button renderSingle = new Button { Text = "生成单张", AutoSize = true };

    /// <summary>
    /// 生成选项：批量生成按钮，文字跟着条数变。
    /// </summary>
    private readonly Button renderBatch = new Button { Text = "批量生成 10 条", AutoSize = true };

    /// <summary>
    /// 右边的四个页签。
    /// </summary>
    private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };

    /// <summary>
    /// 页签：预览生成结果。
    /// </summary>
    private readonly TabPage previewTab = new TabPage("预览");

    /// <summary>
    /// 页签：按当前设置生成的调用代码。
    /// </summary>
    private readonly TabPage codeTab = new TabPage("调用代码");

    /// <summary>
    /// 页签：调用日志（来自 CallCompleted 事件）。
    /// </summary>
    private readonly TabPage logTab = new TabPage("调用日志");

    /// <summary>
    /// 页签：最近一次调用的响应信息或错误详情。
    /// </summary>
    private readonly TabPage responseTab = new TabPage("响应信息");

    /// <summary>
    /// 预览上方的一行结果摘要。
    /// </summary>
    private readonly Label resultInfo = new Label { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };

    /// <summary>
    /// 放预览图（或 ZPL 文本）的面板，内容超出时滚动。
    /// </summary>
    private readonly Panel previewHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(0xF3, 0xF5, 0xF8), AutoScroll = true };

    /// <summary>
    /// 多张结果时预览下方的缩略图条；只有一张时高度为 0。
    /// </summary>
    private readonly FlowLayoutPanel thumbnails = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 0, AutoScroll = true, BackColor = SystemColors.Control };

    /// <summary>
    /// 预览缩放：适应窗口、100%、50%。
    /// </summary>
    private readonly ComboBox zoom = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };

    /// <summary>
    /// 调用代码文本框（只读）。
    /// </summary>
    private readonly TextBox codeText = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = Mono, BackColor = SystemColors.Window };

    /// <summary>
    /// 调用代码显示批量写法。
    /// </summary>
    private readonly CheckBox codeBatch = new CheckBox { Text = "批量写法", AutoSize = true, Margin = new Padding(12, 6, 3, 3) };

    /// <summary>
    /// 调用日志列表，最新的在最上面。
    /// </summary>
    private readonly ListView logList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };

    /// <summary>
    /// 响应信息列表：属性和值两列。
    /// </summary>
    private readonly ListView responseList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };

    /// <summary>
    /// 菜单：保存结果（有结果时可用）。
    /// </summary>
    private readonly ToolStripMenuItem saveMenu = new ToolStripMenuItem("保存结果…") { ShortcutKeys = Keys.Control | Keys.S, Enabled = false };

    /// <summary>
    /// 菜单：用默认程序打开结果（有结果时可用）。
    /// </summary>
    private readonly ToolStripMenuItem openMenu = new ToolStripMenuItem("用默认程序打开") { Enabled = false };

    /// <summary>
    /// 菜单：发送到打印机（有结果时可用）。
    /// </summary>
    private readonly ToolStripMenuItem printMenu = new ToolStripMenuItem("发送到打印机…") { ShortcutKeys = Keys.Control | Keys.P, Enabled = false };

    /// <summary>
    /// 状态栏：当前操作的进度和结果。
    /// </summary>
    private readonly ToolStripStatusLabel status = new ToolStripStatusLabel("就绪") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    /// <summary>
    /// 状态栏：当前连的服务地址。
    /// </summary>
    private readonly ToolStripStatusLabel endpoint = new ToolStripStatusLabel();

    /// <summary>
    /// 预览工具栏上要有结果才能用的按钮。
    /// </summary>
    private readonly List<Button> resultButtons = new List<Button>();

    /// <summary>
    /// 摆好界面、绑定事件，显示空结果。
    /// </summary>
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

    /// <summary>
    /// SDK 的版本号（去掉 + 后面的提交号）。
    /// </summary>
    private static string SdkVersion
    {
        get
        {
            var attribute = typeof(LabelServiceClient).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault();
            return attribute?.InformationalVersion.Split('+')[0] ?? typeof(LabelServiceClient).Assembly.GetName().Version?.ToString() ?? "";
        }
    }

    /// <summary>
    /// 当前运行时的名称，如 .NET Framework 4.8、.NET 10.0.0。
    /// </summary>
    private static string RuntimeName =>
#if NETFRAMEWORK
        ".NET Framework 4.8";
#else
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
#endif
}
