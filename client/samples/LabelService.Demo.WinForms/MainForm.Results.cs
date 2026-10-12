using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 主窗口里显示结果的部分：预览（PNG、ZPL 黑白效果、批量缩略图；PDF 交给默认程序）、响应信息、保存、打开、打印、关于。
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>
    /// 最多预览几张（批量结果只解码前面这些）。
    /// </summary>
    private const int MaxPreviewImages = 24;

    /// <summary>
    /// 当前结果解码出的预览图，换结果时释放。
    /// </summary>
    private readonly List<Image> images = new List<Image>();

    /// <summary>
    /// 当前显示的结果；没有结果时为 null。
    /// </summary>
    private LabelFile? result;

    /// <summary>
    /// 预览中显示的是第几张，从 0 开始。
    /// </summary>
    private int current;

    /// <summary>
    /// 显示一个结果：摘要、预览（PNG 图片、ZPL 文本加打印效果、PDF 和压缩包给出打开按钮）、缩略图和响应信息。
    /// </summary>
    /// <param name="file">结果；null 表示清空，显示提示文字。</param>
    private void ShowResult(LabelFile? file)
    {
        result = file;

        // 先拿掉还引用旧图片的控件再释放图片，否则 PictureBox 重绘时访问已释放的 Image 会抛异常
        ClearControls(previewHost);
        ClearControls(thumbnails);
        foreach (var image in images)
        {
            image.Dispose();
        }

        images.Clear();
        current = 0;
        foreach (var button in resultButtons)
        {
            button.Enabled = file is not null;
        }

        saveMenu.Enabled = openMenu.Enabled = printMenu.Enabled = file is not null;
        responseList.Items.Clear();
        responseList.Groups.Clear();
        if (file is null)
        {
            resultInfo.Text = "";
            SetPreview(Message("点左侧“生成单张”或“批量生成”，结果显示在这里"));
            thumbnails.Height = 0;
            return;
        }

        resultInfo.ForeColor = Color.FromArgb(0x30, 0x79, 0x5A);
        resultInfo.Text = $"200 OK · {file.FileName} · {FormatSize(file.Content.Length)} · v{file.TemplateVersion} · {file.LabelCount} 张 · {file.Dpi} dpi"
            + (file.ServerDuration is { } server ? $" · 服务端 {server.TotalMilliseconds:0.0} ms" : "")
            + (file.TraceId is null ? "" : " · traceId " + Short(file.TraceId));
        var zplText = file.Format == LabelFormat.Zpl && !file.IsZip ? file.GetZplText() : null;
        LoadImages(file, zplText);
        ShowLabelFile(file);
        if (zplText is not null)
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            split.Panel1.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = Mono, Text = zplText.Length > 20000 ? zplText.Substring(0, 20000) + "\r\n…" : zplText.Replace("\n", "\r\n") });
            split.Panel2.Controls.Add(Picture());
            split.Panel2.Controls.Add(new Label { Text = $"打印效果（{file.Dpi} dpi 黑白位图，^GFA 指令）", Dock = DockStyle.Bottom, Height = Px(22), TextAlign = ContentAlignment.MiddleCenter });
            SetPreview(split);
            split.SplitterDistance = Math.Max(Px(120), split.Width / 3);
        }
        else if (images.Count > 0)
        {
            SetPreview(Picture());
        }
        else
        {
            var open = new Button { Text = "用默认程序打开", AutoSize = true, Anchor = AnchorStyles.None };
            open.Click += (_, _) => OpenResult();
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            panel.Controls.Add(Message(file.Format == LabelFormat.Pdf
                ? $"PDF 共 {file.LabelCount} 张。WinForms 里不直接显示 PDF，用默认程序打开查看或打印。"
                : "压缩包里的文件不能预览，保存后查看。"), 0, 0);
            panel.Controls.Add(open, 0, 1);
            SetPreview(panel);
        }

        BuildThumbnails();
        tabs.SelectedTab = previewTab;
    }

    /// <summary>
    /// 把结果解码成预览图：ZPL 按指令画黑白位图，ZIP 逐个解出 PNG 或 ZPL，单张 PNG 直接解码；PDF 不解码。
    /// </summary>
    /// <param name="file">结果。</param>
    /// <param name="zplText">单个 ZPL 文件的文本；不是 ZPL 时为 null。</param>
    private void LoadImages(LabelFile file, string? zplText)
    {
        if (zplText is not null)
        {
            images.AddRange(ZplPreview.Render(zplText, MaxPreviewImages));
        }
        else if (file.IsZip)
        {
            using var zip = new ZipArchive(file.OpenRead(), ZipArchiveMode.Read);
            foreach (var entry in zip.Entries.Take(MaxPreviewImages))
            {
                using var stream = new MemoryStream();
                using (var entryStream = entry.Open())
                {
                    entryStream.CopyTo(stream);
                }

                if (entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    images.Add(Image.FromStream(new MemoryStream(stream.ToArray())));
                }
                else if (entry.Name.EndsWith(".zpl", StringComparison.OrdinalIgnoreCase))
                {
                    images.AddRange(ZplPreview.Render(System.Text.Encoding.UTF8.GetString(stream.ToArray()), 1));
                }
            }
        }
        else if (file.Format == LabelFormat.Png)
        {
            images.Add(Image.FromStream(new MemoryStream(file.Content)));
        }
    }

    /// <summary>
    /// 显示当前这张预览图的图片框。
    /// </summary>
    /// <returns>图片框（已按缩放设置摆好）。</returns>
    private PictureBox Picture()
    {
        var picture = new PictureBox { BackColor = Color.White, Image = images.Count > current ? images[current] : null, Tag = "preview" };
        ApplyZoom(picture);
        return picture;
    }

    /// <summary>
    /// 缩放设置变化后重新摆放预览里的全部图片框。
    /// </summary>
    private void LayoutPreview()
    {
        foreach (var picture in previewHost.Controls.Cast<Control>().SelectMany(Descendants).OfType<PictureBox>())
        {
            ApplyZoom(picture);
        }
    }

    /// <summary>
    /// 按缩放设置摆放图片框：适应窗口时铺满并等比缩放，100%、50% 时按原图尺寸缩放、超出时滚动。
    /// </summary>
    /// <param name="picture">图片框。</param>
    private void ApplyZoom(PictureBox picture)
    {
        if (zoom.SelectedIndex == 0 || picture.Image is null)
        {
            picture.Dock = DockStyle.Fill;
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            return;
        }

        var scale = zoom.SelectedIndex == 1 ? 1.0 : 0.5;
        picture.Dock = DockStyle.None;
        picture.SizeMode = PictureBoxSizeMode.StretchImage;
        picture.Size = new Size((int)(picture.Image.Width * scale), (int)(picture.Image.Height * scale));
    }

    /// <summary>
    /// 多于一张时在预览下方放缩略图，点一下切换；只预览了一部分时写明总张数。
    /// </summary>
    private void BuildThumbnails()
    {
        ClearControls(thumbnails);
        thumbnails.Height = images.Count > 1 ? Px(96) : 0;
        if (images.Count <= 1)
        {
            return;
        }

        for (var i = 0; i < images.Count; i++)
        {
            var index = i;
            var thumb = new PictureBox { Image = images[i], SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(Px(110), Px(70)), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, BackColor = Color.White, Margin = new Padding(Px(4)) };
            thumb.Click += (_, _) => ShowImage(index);
            thumbnails.Controls.Add(thumb);
        }

        if (result is { } file && file.LabelCount > images.Count)
        {
            thumbnails.Controls.Add(new Label { Text = $"共 {file.LabelCount} 张，预览前 {images.Count} 张", AutoSize = true, Margin = new Padding(Px(8), Px(30), 0, 0), ForeColor = SystemColors.GrayText });
        }
    }

    /// <summary>
    /// 预览里换成第 <paramref name="index"/> 张（点缩略图）。
    /// </summary>
    /// <param name="index">第几张，从 0 开始。</param>
    private void ShowImage(int index)
    {
        current = index;
        foreach (var picture in previewHost.Controls.Cast<Control>().SelectMany(Descendants).OfType<PictureBox>())
        {
            picture.Image = images[index];
            ApplyZoom(picture);
        }
    }

    /// <summary>
    /// 显示调用失败：SDK 的 LabelServiceException 列出状态码、错误码、跟踪 ID、是否重试和逐条错误；
    /// 本地校验的异常说明请求没有发出，并对照服务端的错误码。
    /// </summary>
    /// <param name="exception">异常。</param>
    private void ShowError(Exception exception)
    {
        ShowResult(null);
        resultInfo.ForeColor = Color.FromArgb(0xB8, 0x58, 0x62);
        if (exception is LabelServiceException ex)
        {
            resultInfo.Text = $"{(ex.StatusCode is { } code ? code + " " : "")}{ex.ErrorCode} · {ex.Message}";
            var group = Group("调用失败，SDK 抛出 LabelServiceException");
            AddProperty(group, "StatusCode", ex.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "（网络层错误，没有 HTTP 状态码）");
            AddProperty(group, "ErrorCode", ex.ErrorCode);
            AddProperty(group, "Message", ex.Message);
            AddProperty(group, "TraceId", ex.TraceId ?? "（网络层错误，没有服务端 traceId）");
            AddProperty(group, "IsTransient", ex.IsTransient ? "true（SDK 已自动重试）" : "false（不会重试，需要修改请求）");
            if (ex.Errors.Count > 0)
            {
                var errors = Group("Errors（逐条错误）");
                foreach (var error in ex.Errors)
                {
                    AddProperty(errors, $"{(error.Index is { } i ? "第 " + (i + 1) + " 条 · " : "")}{error.Field ?? "—"}", $"{error.Code} · {error.Message}");
                }
            }
        }
        else
        {
            resultInfo.Text = "请求没有发出：" + PlainMessage(exception);
            var group = Group($"请求没有发出：SDK 在本地校验参数时抛出 {exception.GetType().Name}，不会占用服务端配额");
            AddProperty(group, "ParamName", (exception as ArgumentException)?.ParamName ?? "—");
            AddProperty(group, "Message", PlainMessage(exception));
            AddProperty(group, "对应服务端错误码", ServerCodeFor(exception));
        }

        SetPreview(Message(resultInfo.Text + "\r\n\r\n详细信息见“响应信息”页签。"));
    }

    /// <summary>
    /// SDK 本地预校验拦下的请求如果发到服务端，会返回设计书第 8 节的哪个错误码，方便和服务端的错误对照。
    /// </summary>
    /// <param name="exception">SDK 本地校验抛出的异常。</param>
    /// <returns>对应的服务端错误码；参数不完整、服务端收不到时是一句说明。</returns>
    private static string ServerCodeFor(Exception exception) =>
        exception.Message.Contains("上限") ? LabelErrorCodes.BatchTooLarge
        : exception.Message.Contains("份") || (exception as ArgumentException)?.ParamName?.EndsWith("opies", StringComparison.Ordinal) == true ? LabelErrorCodes.CopiesInvalid
        : exception.Message.Contains("Zip") ? LabelErrorCodes.OutputInvalid
        : exception.Message.Contains("拼版") ? LabelErrorCodes.FormatInvalid
        : exception.Message.Contains("StartSlot") ? LabelErrorCodes.StartSlotInvalid
        : "—（参数不完整，服务端不会收到这个请求）";

    /// <summary>
    /// 去掉 ArgumentException 消息后面附带的参数名：.NET Framework 另起一行写“参数名: x”，.NET 写成“ (Parameter 'x')”。
    /// </summary>
    /// <param name="exception">异常。</param>
    /// <returns>不带参数名的消息。</returns>
    private static string PlainMessage(Exception exception)
    {
        var message = exception.Message;
        if (exception is ArgumentException { ParamName: { } name })
        {
            var line = message.IndexOf('\n');
            message = line >= 0 ? message.Substring(0, line).TrimEnd('\r') : message;
            var suffix = $" (Parameter '{name}')";
            message = message.EndsWith(suffix, StringComparison.Ordinal) ? message.Substring(0, message.Length - suffix.Length) : message;
        }

        return message;
    }

    /// <summary>
    /// 在响应信息里列出 LabelFile 的属性和各阶段耗时。
    /// </summary>
    /// <param name="file">结果。</param>
    private void ShowLabelFile(LabelFile file)
    {
        var group = Group("成功：SDK 返回 LabelFile，这些值来自响应头和响应体");
        AddProperty(group, "FileName", file.FileName);
        AddProperty(group, "ContentType", file.ContentType);
        AddProperty(group, "Format", "LabelFormat." + file.Format + (file.IsZip ? "（ZIP）" : ""));
        AddProperty(group, "Content.Length", file.Content.Length.ToString("N0", CultureInfo.InvariantCulture) + " 字节");
        AddProperty(group, "TemplateVersion", file.TemplateVersion?.ToString(CultureInfo.InvariantCulture) ?? "—");
        AddProperty(group, "LabelCount", file.LabelCount.ToString(CultureInfo.InvariantCulture));
        AddProperty(group, "Dpi", file.Dpi.ToString(CultureInfo.InvariantCulture));
        AddProperty(group, "TraceId", file.TraceId ?? "—");
        AddProperty(group, "RequestId", file.RequestId ?? "—");
        AddProperty(group, "ServerDuration", file.ServerDuration is { } server ? $"{server.TotalMilliseconds:0.0} ms" : "—");
        AddProperty(group, "Warnings", file.Warnings.Count > 0 ? string.Join("；", file.Warnings) : "（无）");
        var timing = Group("ServerTiming（Server-Timing 响应头，各阶段毫秒）");
        foreach (var stage in file.ServerTiming)
        {
            AddProperty(timing, stage.Key, stage.Value.ToString("0.0", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// 在响应信息里加一个分组。
    /// </summary>
    /// <param name="header">分组标题。</param>
    /// <returns>分组。</returns>
    private ListViewGroup Group(string header)
    {
        var group = new ListViewGroup(header);
        responseList.Groups.Add(group);
        return group;
    }

    /// <summary>
    /// 在响应信息的分组里加一行。
    /// </summary>
    /// <param name="group">分组。</param>
    /// <param name="name">属性名。</param>
    /// <param name="value">值。</param>
    private void AddProperty(ListViewGroup group, string name, string value) =>
        responseList.Items.Add(new ListViewItem(new[] { name, value }, group));

    /// <summary>
    /// 换掉预览区的内容（旧内容释放）。
    /// </summary>
    /// <param name="content">新内容；图片框保留自己的停靠方式，其他铺满。</param>
    private void SetPreview(Control content)
    {
        ClearControls(previewHost);
        content.Dock = content is PictureBox ? content.Dock : DockStyle.Fill;
        previewHost.Controls.Add(content);
    }

    /// <summary>
    /// 移除并释放子控件（Controls.Clear 只移除不释放，窗口句柄会越积越多）。
    /// </summary>
    /// <param name="host">容器控件。</param>
    private static void ClearControls(Control host)
    {
        while (host.Controls.Count > 0)
        {
            host.Controls[0].Dispose();
        }
    }

    /// <summary>
    /// 预览区居中的灰色提示文字。
    /// </summary>
    /// <param name="text">提示。</param>
    /// <returns>标签。</returns>
    private static Label Message(string text) =>
        new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText };

    /// <summary>
    /// 选位置保存当前结果（演示 LabelFile.Save）。
    /// </summary>
    private void SaveResult()
    {
        if (result is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog { FileName = result.FileName, Filter = "全部文件|*.*", Title = "保存文件（file.Save(path)）" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            result.Save(dialog.FileName);
            status.Text = "已保存到 " + dialog.FileName;
        }
    }

    /// <summary>
    /// 把当前结果存到临时目录，用系统默认程序打开。
    /// </summary>
    private void OpenResult()
    {
        if (result is null)
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "LabelServiceDemo", result.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        result.Save(path);
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            MessageBox.Show(this, $"没有能打开 {Path.GetExtension(path)} 文件的程序：{ex.Message}\r\n文件在 {path}", "用默认程序打开", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// 打开打印窗口发送当前结果。
    /// </summary>
    private void PrintResult()
    {
        if (result is not null)
        {
            using var dialog = new PrintForm(result, images.Count > current ? images[current] : null);
            dialog.ShowDialog(this);
        }
    }

    /// <summary>
    /// 显示关于：SDK 版本、目标框架、依赖和当前运行时。
    /// </summary>
    private void ShowAbout() =>
        MessageBox.Show(this,
            $"标签服务 SDK 调用演示　{SdkVersion}\r\n\r\nLabelService.Client.dll {SdkVersion}\r\n目标框架：net461 · netstandard2.0 · net8.0（.NET Framework 4.6.1 起）\r\n依赖：无（只用 .NET 自带的组件）\r\n\r\n当前运行在 {RuntimeName}。演示程序同时提供 .NET Framework 4.8 和 .NET 10 两个版本，源码随 SDK 一起发给对接方。",
            "关于 LabelServiceDemo", MessageBoxButtons.OK, MessageBoxIcon.Information);

    /// <summary>
    /// 控件和它的全部后代控件。
    /// </summary>
    /// <param name="control">起始控件。</param>
    /// <returns>控件本身和后代，深度优先。</returns>
    private static IEnumerable<Control> Descendants(Control control) =>
        new[] { control }.Concat(control.Controls.Cast<Control>().SelectMany(Descendants));

    /// <summary>
    /// 文件大小的显示写法：B、KB 或 MB。
    /// </summary>
    /// <param name="bytes">字节数。</param>
    /// <returns>如 12.3 KB。</returns>
    private static string FormatSize(int bytes) =>
        bytes < 1024 ? bytes + " B" : bytes < 1024 * 1024 ? (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB" : (bytes / 1024.0 / 1024).ToString("0.00", CultureInfo.InvariantCulture) + " MB";

    /// <summary>
    /// 跟踪 ID 太长时（W3C traceparent 格式，00-trace-id-span-id-flags）只显示 trace-id 的前 16 位。
    /// </summary>
    /// <param name="traceId">跟踪 ID。</param>
    /// <returns>缩短后的跟踪 ID。</returns>
    private static string Short(string traceId) => traceId.Length > 20 ? traceId.Substring(3, 16) + "…" : traceId;
}
