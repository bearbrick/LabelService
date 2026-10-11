using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text;

namespace LabelService.Demo.Mock;

/// <summary>
/// 模拟服务生成的文件：用 System.Drawing 画一张示意标签（字段值 + 示意条码），再编码成 PNG、PDF（每页一张 JPEG 图）或 ZPL（^GFA 位图）。
/// </summary>
internal static class MockDocuments
{
    /// <summary>
    /// PNG：一张标签。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="data">字段值。</param>
    /// <returns>PNG 文件内容。</returns>
    public static byte[] Png(MockTemplate template, int dpi, Dictionary<string, string> data)
    {
        using var bitmap = Draw(template, dpi, data);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    /// <summary>
    /// ZPL：每张标签一段 ^XA … ^XZ，份数用 ^PQ。位图不压缩（真实服务端会压缩）。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="labels">每张标签的字段值和份数。</param>
    /// <returns>ZPL 文本的 UTF-8 字节。</returns>
    public static byte[] Zpl(MockTemplate template, int dpi, IEnumerable<(Dictionary<string, string> Data, int Copies)> labels)
    {
        var zpl = new StringBuilder();
        foreach (var (data, copies) in labels)
        {
            using var bitmap = Draw(template, dpi, data);
            // 每行按 8 个像素一个字节打包，暗的像素是 1（黑点），写成十六进制。
            var bytesPerRow = (bitmap.Width + 7) / 8;
            var hex = new StringBuilder(bytesPerRow * bitmap.Height * 2);
            var locked = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[locked.Stride * bitmap.Height];
            System.Runtime.InteropServices.Marshal.Copy(locked.Scan0, pixels, 0, pixels.Length);
            bitmap.UnlockBits(locked);
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x0 = 0; x0 < bytesPerRow * 8; x0 += 8)
                {
                    var value = 0;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        var x = x0 + bit;
                        var i = y * locked.Stride + x * 4;
                        if (x < bitmap.Width && pixels[i] + pixels[i + 1] + pixels[i + 2] < 384)
                        {
                            value |= 0x80 >> bit;
                        }
                    }

                    hex.Append(value.ToString("X2", CultureInfo.InvariantCulture));
                }
            }

            // 一张标签一段 ^XA…^XZ：纸宽、纸长、^GFA 位图、份数。
            var total = bytesPerRow * bitmap.Height;
            zpl.Append("^XA^PW").Append(bitmap.Width).Append("^LL").Append(bitmap.Height)
                .Append("^FO0,0^GFA,").Append(total).Append(',').Append(total).Append(',').Append(bytesPerRow).Append(',')
                .Append(hex).Append("^FS^PQ").Append(copies).Append("^XZ\n");
        }

        return Encoding.UTF8.GetBytes(zpl.ToString());
    }

    /// <summary>
    /// PDF：每份一页，页面尺寸等于标签尺寸，内容是 150 dpi 的 JPEG。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="labels">每张标签的字段值和份数。</param>
    /// <returns>PDF 文件内容。</returns>
    public static byte[] Pdf(MockTemplate template, IEnumerable<(Dictionary<string, string> Data, int Copies)> labels)
    {
        var pages = new List<byte[]>();
        foreach (var (data, copies) in labels)
        {
            using var bitmap = Draw(template, 150, data);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Jpeg);
            var jpeg = stream.ToArray();
            for (var i = 0; i < copies; i++)
            {
                pages.Add(jpeg);
            }
        }

        // 手写一个最小的 PDF：目录、页树，每页一个内容流和一张 JPEG 图片，最后是交叉引用表。
        var width = template.Width / 25.4 * 72;
        var height = template.Height / 25.4 * 72;
        using var pdf = new MemoryStream();
        var offsets = new List<long>();
        void Write(string text) => pdf.Write(Encoding.ASCII.GetBytes(text), 0, Encoding.ASCII.GetByteCount(text));
        void Begin(int id)
        {
            offsets.Add(pdf.Position);
            Write($"{id} 0 obj\n");
        }

        Write("%PDF-1.4\n");
        Begin(1);
        Write("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Begin(2);
        var kids = string.Join(" ", pages.Select((_, i) => $"{3 + i * 3} 0 R"));
        Write($"<< /Type /Pages /Count {pages.Count} /Kids [{kids}] >>\nendobj\n");
        for (var i = 0; i < pages.Count; i++)
        {
            var page = 3 + i * 3;
            var size = string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1:0.##}", width, height);
            Begin(page);
            Write($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {size}] /Resources << /XObject << /Im0 {page + 2} 0 R >> >> /Contents {page + 1} 0 R >>\nendobj\n");
            var content = $"q {size.Split(' ')[0]} 0 0 {size.Split(' ')[1]} 0 0 cm /Im0 Do Q";
            Begin(page + 1);
            Write($"<< /Length {content.Length} >>\nstream\n{content}\nendstream\nendobj\n");
            using var image = Image.FromStream(new MemoryStream(pages[i]));
            Begin(page + 2);
            Write($"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {pages[i].Length} >>\nstream\n");
            pdf.Write(pages[i], 0, pages[i].Length);
            Write("\nendstream\nendobj\n");
        }

        var xref = pdf.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        Write($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }

    /// <summary>
    /// 画一张示意标签：外框、模板名、字段值、按第一个“编号”类字段生成的示意条码、模拟服务水印。
    /// </summary>
    /// <param name="template">模板。</param>
    /// <param name="dpi">分辨率。</param>
    /// <param name="data">字段值。</param>
    /// <returns>位图，由调用方释放。</returns>
    private static Bitmap Draw(MockTemplate template, int dpi, Dictionary<string, string> data)
    {
        var dotsPerMm = dpi / 25.4f;
        var bitmap = new Bitmap(Math.Max(1, (int)Math.Round(template.Width * dotsPerMm)), Math.Max(1, (int)Math.Round(template.Height * dotsPerMm)));
        bitmap.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float Mm(double mm) => (float)(mm * dotsPerMm);
        using var pen = new Pen(Color.Black, Mm(0.4));
        g.DrawRectangle(pen, Mm(1), Mm(1), bitmap.Width - Mm(2), bitmap.Height - Mm(2));
        var scale = Math.Min(template.Width / 100, template.Height / 60);
        using var title = new Font("Microsoft YaHei", Mm(4.2 * scale), FontStyle.Bold, GraphicsUnit.Pixel);
        using var body = new Font("Microsoft YaHei", Mm(3.2 * scale), GraphicsUnit.Pixel);
        using var small = new Font("Microsoft YaHei", Mm(2.2 * scale), GraphicsUnit.Pixel);
        g.DrawString(template.Name, title, Brushes.Black, Mm(4), Mm(3));
        g.DrawString("模拟服务 · 示意图", small, Brushes.Gray, bitmap.Width - g.MeasureString("模拟服务 · 示意图", small).Width - Mm(4), Mm(4));

        // 字段值两列排列。
        var y = Mm(11 * scale);
        var column = 0;
        foreach (var field in template.Fields)
        {
            data.TryGetValue(field.Key, out var value);
            var x = column == 0 ? Mm(4) : bitmap.Width / 2f;
            g.DrawString($"{field.Name}：{value}", body, Brushes.Black, x, y);
            column = 1 - column;
            if (column == 0)
            {
                y += Mm(5.5 * scale);
            }
        }

        // 下面画一个示意条码：取第一个带数字的字段值，按字节的位画粗细不同的条（不是真的条码）。
        var code = template.Fields.Select(field => data.TryGetValue(field.Key, out var value) ? value : "").FirstOrDefault(value => value.Any(char.IsDigit)) ?? template.Code;
        var barTop = y + Mm(6 * scale);
        var barHeight = Math.Max(Mm(8), bitmap.Height - barTop - Mm(10 * scale));
        var module = Math.Max(1, (int)Mm(0.33 * scale));
        var bars = Encoding.UTF8.GetBytes(code);
        var left = Mm(4);
        foreach (var value in bars.Concat(Enumerable.Reverse(bars)))
        {
            for (var bit = 0; bit < 6 && left < bitmap.Width / 2f; bit++)
            {
                var width = module * (1 + ((value >> bit) & 1) * 2);
                if (bit % 2 == 0)
                {
                    g.FillRectangle(Brushes.Black, left, barTop, width, barHeight);
                }

                left += width;
            }
        }

        g.DrawString(code, small, Brushes.Black, Mm(4), barTop + barHeight + Mm(1));
        return bitmap;
    }
}
