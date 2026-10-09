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
    private const int MaxPreviewImages = 24;
    private readonly List<Image> images = new List<Image>();
    private LabelFile? result;
    private int current;

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
            split.Panel2.Controls.Add(new Label { Text = $"打印效果（{file.Dpi} dpi 黑白位图，^GFA 指令）", Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleCenter });
            SetPreview(split);
            split.SplitterDistance = Math.Max(120, split.Width / 3);
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

    private PictureBox Picture()
    {
        var picture = new PictureBox { BackColor = Color.White, Image = images.Count > current ? images[current] : null, Tag = "preview" };
        ApplyZoom(picture);
        return picture;
    }

    private void LayoutPreview()
    {
        foreach (var picture in previewHost.Controls.Cast<Control>().SelectMany(Descendants).OfType<PictureBox>())
        {
            ApplyZoom(picture);
        }
    }

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

    private void BuildThumbnails()
    {
        ClearControls(thumbnails);
        thumbnails.Height = images.Count > 1 ? 96 : 0;
        if (images.Count <= 1)
        {
            return;
        }

        for (var i = 0; i < images.Count; i++)
        {
            var index = i;
            var thumb = new PictureBox { Image = images[i], SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(110, 70), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, BackColor = Color.White, Margin = new Padding(4) };
            thumb.Click += (_, _) => ShowImage(index);
            thumbnails.Controls.Add(thumb);
        }

        if (result is { } file && file.LabelCount > images.Count)
        {
            thumbnails.Controls.Add(new Label { Text = $"共 {file.LabelCount} 张，预览前 {images.Count} 张", AutoSize = true, Margin = new Padding(8, 30, 0, 0), ForeColor = SystemColors.GrayText });
        }
    }

    /// <summary>预览里换成第 <paramref name="index"/> 张（点缩略图）。</summary>
    private void ShowImage(int index)
    {
        current = index;
        foreach (var picture in previewHost.Controls.Cast<Control>().SelectMany(Descendants).OfType<PictureBox>())
        {
            picture.Image = images[index];
            ApplyZoom(picture);
        }
    }

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

    private ListViewGroup Group(string header)
    {
        var group = new ListViewGroup(header);
        responseList.Groups.Add(group);
        return group;
    }

    private void AddProperty(ListViewGroup group, string name, string value) =>
        responseList.Items.Add(new ListViewItem(new[] { name, value }, group));

    private void SetPreview(Control content)
    {
        ClearControls(previewHost);
        content.Dock = content is PictureBox ? content.Dock : DockStyle.Fill;
        previewHost.Controls.Add(content);
    }

    /// <summary>移除并释放子控件（Controls.Clear 只移除不释放，窗口句柄会越积越多）。</summary>
    private static void ClearControls(Control host)
    {
        while (host.Controls.Count > 0)
        {
            host.Controls[0].Dispose();
        }
    }

    private static Label Message(string text) =>
        new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText };

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

    private void PrintResult()
    {
        if (result is not null)
        {
            using var dialog = new PrintForm(result, images.Count > current ? images[current] : null);
            dialog.ShowDialog(this);
        }
    }

    private void ShowAbout() =>
        MessageBox.Show(this,
            $"标签服务 SDK 调用演示　{SdkVersion}\r\n\r\nLabelService.Client.dll {SdkVersion}\r\n目标框架：net462 · netstandard2.0 · net8.0\r\n依赖：System.Text.Json 8.x（仅 .NET Framework 和 .NET Standard 版本）\r\n\r\n当前运行在 {RuntimeName}。演示程序同时提供 .NET Framework 4.8 和 .NET 10 两个版本，源码随 SDK 一起发给对接方。",
            "关于 LabelServiceDemo", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static IEnumerable<Control> Descendants(Control control) =>
        new[] { control }.Concat(control.Controls.Cast<Control>().SelectMany(Descendants));

    private static string FormatSize(int bytes) =>
        bytes < 1024 ? bytes + " B" : bytes < 1024 * 1024 ? (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB" : (bytes / 1024.0 / 1024).ToString("0.00", CultureInfo.InvariantCulture) + " MB";

    private static string Short(string traceId) => traceId.Length > 20 ? traceId.Substring(3, 16) + "…" : traceId;
}
