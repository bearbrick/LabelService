using System.Drawing;
using System.Windows.Forms;

namespace LabelService.Demo;

/// <summary>
/// 主窗口的界面布局：标题栏、菜单、状态栏、左栏的三组设置、右栏的四个页签，以及摆控件用的几个小工具。
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>
    /// 顶部深色标题栏：标题、说明、SDK 版本和运行时。
    /// </summary>
    /// <returns>标题栏。</returns>
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

    /// <summary>
    /// 菜单栏：文件、工具、帮助。
    /// </summary>
    /// <returns>菜单栏。</returns>
    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var exit = new ToolStripMenuItem("退出(&X)", null, (_, _) => Close()) { ShortcutKeys = Keys.Alt | Keys.F4 };
        menu.Items.Add(new ToolStripMenuItem("文件(&F)", null, saveMenu, openMenu, new ToolStripSeparator(), exit));
        var clearLog = new ToolStripMenuItem("清空调用日志", null, (_, _) => logList.Items.Clear());
        menu.Items.Add(new ToolStripMenuItem("工具(&T)", null, new ToolStripMenuItem("测试连接", null, async (_, _) => await TestConnectionAsync()), printMenu,
            new ToolStripMenuItem("选择其他打印机…", null, (_, _) => PrintResultWithSelection()), new ToolStripSeparator(), clearLog));
        var showCode = new ToolStripMenuItem("查看调用代码", null, (_, _) => tabs.SelectedTab = codeTab);
        var about = new ToolStripMenuItem("关于 LabelServiceDemo", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripMenuItem("帮助(&H)", null, showCode, about));
        saveMenu.Click += (_, _) => SaveResult();
        openMenu.Click += (_, _) => OpenResult();
        printMenu.Click += async (_, _) => await PrintResultAsync();
        return menu;
    }

    /// <summary>
    /// 状态栏：操作状态、服务地址、SDK 版本、运行时。
    /// </summary>
    /// <returns>状态栏。</returns>
    private StatusStrip BuildStatus()
    {
        var strip = new StatusStrip();
        strip.Items.Add(status);
        strip.Items.Add(endpoint);
        strip.Items.Add(new ToolStripStatusLabel("SDK " + SdkVersion) { BorderSides = ToolStripStatusLabelBorderSides.Left });
        strip.Items.Add(new ToolStripStatusLabel(RuntimeName) { BorderSides = ToolStripStatusLabelBorderSides.Left });
        return strip;
    }

    /// <summary>
    /// 左栏：连接设置、模板与数据（占满剩余高度）、生成选项三组。
    /// </summary>
    /// <returns>左栏。</returns>
    private Control BuildLeft()
    {
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        // 列宽跟着左栏走（不按内容撑宽），出现纵向滚动条时不会再多出横向滚动条
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
        AddRow(data, "模板编码", Line(templateCode, Flow(refreshTemplates, readSchema)));
        data.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        data.Controls.Add(dataTabs, 0, data.RowCount);
        data.SetColumnSpan(dataTabs, 2);
        data.RowCount++;
        data.Controls.Add(schemaInfo, 0, data.RowCount);
        data.SetColumnSpan(schemaInfo, 2);
        data.RowCount++;
        var dataGroup = Group("模板与数据", data);
        dataGroup.Dock = DockStyle.Fill;
        left.Controls.Add(dataGroup, 0, 1);
        // 字段、名称、类型、必填按内容（含表头）定宽，不会被截成“num...”“必”；值占满剩下的宽度
        var fitContent = DataGridViewAutoSizeColumnMode.AllCells;
        fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "key", HeaderText = "字段", ReadOnly = true, AutoSizeMode = fitContent });
        fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "名称", ReadOnly = true, AutoSizeMode = fitContent });
        fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "类型", ReadOnly = true, AutoSizeMode = fitContent });
        fields.Columns.Add(new DataGridViewCheckBoxColumn { Name = "required", HeaderText = "必填", ReadOnly = true, AutoSizeMode = fitContent });
        fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "value", HeaderText = "值", MinimumWidth = 100 });
        singleTab.Controls.Add(fields);
        batchTab.Controls.Add(batchRows);
        batchTab.Controls.Add(Toolbar(addRow, removeRows, pasteRows, clearRows));
        batchTab.Controls.Add(BatchHint());
        dataTabs.TabPages.AddRange(new[] { singleTab, batchTab });

        var options = Grid();
        AddRow(options, "格式", Flow(png, pdf, zpl, new Label { Text = "DPI", AutoSize = true, Margin = new Padding(12, 6, 3, 0) }, dpi));
        AddRow(options, "份数", Flow(copies, new Label { Text = "单张生成时每张打几份（1–100）", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(6, 6, 3, 0) }));
        AddRow(options, "", Flow(renderSingle, renderBatch));
        left.Controls.Add(Group("生成选项", options), 0, 2);

        // 窗口很矮时字段表格会被压得一行都看不到：左栏放进可滚动的外框，高度至少让表格显示 6 行，不够时出现纵向滚动条。
        // 左栏停靠在顶部、高度自己算：停靠在顶部的控件只参与纵向滚动范围，宽度始终跟着外框，不会多出横向滚动条。
        var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        left.Dock = DockStyle.Top;
        left.Margin = Padding.Empty;
        scroller.Controls.Add(left);
        var minimumHeight = 0;
        EventHandler place = (_, _) => left.Height = Math.Max(scroller.ClientSize.Height, minimumHeight);
        scroller.Resize += place;
        Shown += (_, _) =>
        {
            // 窗口显示出来、按默认大小排好之后，按其他内容的实际高度算出最小高度（Load 时布局还没定下来，量出来偏小）
            place(scroller, EventArgs.Empty);
            left.PerformLayout();
            var rowHeight = Math.Max(fields.RowTemplate.Height, fields.Font.Height + 9);
            minimumHeight = left.Height - fields.Height + fields.ColumnHeadersHeight + (rowHeight * 6) + 2;
            place(scroller, EventArgs.Empty);
        };
        return scroller;
    }

    /// <summary>
    /// 右栏：预览、调用代码、调用日志、响应信息四个页签。
    /// </summary>
    /// <returns>右栏。</returns>
    private Control BuildRight()
    {
        var previewTools = Toolbar(
            ResultButton("保存文件…", (_, _) => SaveResult()),
            ResultButton("用默认程序打开", (_, _) => OpenResult()),
            ResultButton("静默打印", async (_, _) => await PrintResultAsync()),
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
        codeTab.Controls.Add(Toolbar(copy, codeBatch, Hint("根据左侧当前设置生成，C# 7.3 起可用（.NET Framework 4.6.1+ / .NET 8+）")));

        foreach (var (text, width) in new[] { ("时间", 70), ("操作", 100), ("模板", 120), ("结果", 140), ("总耗时", 70), ("服务端", 70), ("重试", 44), ("requestId", 90), ("traceId", 120), ("说明", 200) })
        {
            // 列宽不跟着窗口自动缩放，按屏幕缩放换算
            logList.Columns.Add(text, Px(width));
        }

        var clear = new Button { Text = "清空", AutoSize = true };
        clear.Click += (_, _) => logList.Items.Clear();
        logTab.Controls.Add(logList);
        logTab.Controls.Add(Toolbar(clear, Hint("来自 LabelServiceClient.CallCompleted 事件；单张服务端超过 100 ms 标为慢调用")));

        responseList.Columns.Add("属性", Px(180));
        responseList.Columns.Add("值", Px(560));
        responseTab.Controls.Add(responseList);

        tabs.TabPages.AddRange(new[] { previewTab, codeTab, logTab, responseTab });
        return tabs;
    }

    /// <summary>
    /// 批量数据下方的说明，按宽度自动折行。
    /// </summary>
    /// <returns>说明文字。</returns>
    private Label BatchHint()
    {
        var hint = new Label
        {
            Text = "一行一张标签，每行的数据都由调用方给全，SDK 原样发给服务端。可以在 Excel 里复制多行，选中起始格子后点“粘贴”或按 Ctrl+V；第一行是字段名时替换现有数据、按字段名对列。",
            Dock = DockStyle.Bottom,
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(2, 4, 2, 0),
        };

        // Label 停靠时不会自己折行算高度：宽度变了按文字重新算
        hint.SizeChanged += (_, _) =>
        {
            var height = TextRenderer.MeasureText(hint.Text, hint.Font, new Size(Math.Max(hint.Width - hint.Padding.Horizontal, 1), 0), TextFormatFlags.WordBreak).Height + hint.Padding.Vertical;
            if (hint.Height != height)
            {
                hint.Height = height;
            }
        };
        return hint;
    }

    /// <summary>
    /// 把 100% 缩放时的像素换成当前屏幕的像素。窗口创建时摆好的控件由自动缩放放大；之后新建的控件和列表的列宽要用它换算。
    /// </summary>
    /// <param name="value">100% 缩放时的像素。</param>
    /// <returns>当前屏幕的像素。</returns>
    private int Px(int value) => LogicalToDeviceUnits(value);

    /// <summary>
    /// 预览工具栏上的按钮：没有结果时禁用。
    /// </summary>
    /// <param name="text">按钮文字。</param>
    /// <param name="click">点击时执行。</param>
    /// <returns>按钮。</returns>
    private Button ResultButton(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Enabled = false };
        button.Click += click;
        resultButtons.Add(button);
        return button;
    }

    /// <summary>
    /// 两列的表单网格：左列标签固定 76 像素，右列占满。
    /// </summary>
    /// <returns>网格。</returns>
    private static TableLayoutPanel Grid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    /// <summary>
    /// 在表单网格末尾加一行。
    /// </summary>
    /// <param name="grid">表单网格。</param>
    /// <param name="label">左列标签；空字符串表示没有标签。</param>
    /// <param name="control">右列控件。</param>
    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, grid.RowCount);
        grid.Controls.Add(control, 1, grid.RowCount);
        grid.RowCount++;
    }

    /// <summary>
    /// 带标题的分组框。
    /// </summary>
    /// <param name="text">标题。</param>
    /// <param name="content">内容。</param>
    /// <returns>分组框。</returns>
    private static GroupBox Group(string text, Control content)
    {
        var group = new GroupBox { Text = text, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 4, 6, 6) };
        group.Controls.Add(content);
        return group;
    }

    /// <summary>
    /// 一行两个控件：左边占满，右边按内容宽度。
    /// </summary>
    /// <param name="fill">左边的控件。</param>
    /// <param name="right">右边的控件。</param>
    /// <returns>一行。</returns>
    private static Control Line(Control fill, Control right)
    {
        var line = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        line.Controls.Add(fill, 0, 0);
        line.Controls.Add(right, 1, 0);
        return line;
    }

    /// <summary>
    /// 横排一组控件，不换行。
    /// </summary>
    /// <param name="controls">控件。</param>
    /// <returns>横排面板。</returns>
    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
        flow.Controls.AddRange(controls);
        return flow;
    }

    /// <summary>
    /// 页签顶部的工具栏。
    /// </summary>
    /// <param name="controls">工具栏上的控件。</param>
    /// <returns>工具栏。</returns>
    private static FlowLayoutPanel Toolbar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, AutoSize = true, Padding = new Padding(4, 3, 4, 0), WrapContents = false };
        bar.Controls.AddRange(controls);
        return bar;
    }

    /// <summary>
    /// 灰色的提示文字。
    /// </summary>
    /// <param name="text">提示。</param>
    /// <returns>标签。</returns>
    private static Label Hint(string text) =>
        new Label { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(12, 7, 3, 0) };
}
