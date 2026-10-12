#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;

namespace LabelService.Client.Checks;

/// <summary>
/// 打印检查的独立文件样本，不使用服务端生成器，也不调用实际打印机。
/// </summary>
internal static class PrintFixtures
{
    /// <summary>
    /// 生成每页两英寸乘一英寸的实色 PDF，含真实对象表和交叉引用表。
    /// </summary>
    /// <param name="colors">各页的 PDF 颜色指令。</param>
    /// <returns>完整 PDF。</returns>
    public static byte[] Pdf(params string[] colors)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count " + colors.Length + " /Kids ["
                + string.Join(" ", Enumerable.Range(0, colors.Length).Select(index => (3 + index * 2) + " 0 R")) + "] >>",
        };
        for (var index = 0; index < colors.Length; index++)
        {
            objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 72] /Resources << >> /Contents " + (4 + index * 2) + " 0 R >>");
            var content = colors[index] + " rg 0 0 144 72 re f\n";
            objects.Add("<< /Length " + content.Length + " >>\nstream\n" + content + "endstream");
        }

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append("xref\n0 ").Append(offsets.Count).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer\n<< /Size ").Append(offsets.Count).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>
    /// 生成一百乘五十像素的实色 PNG。
    /// </summary>
    /// <param name="color">图片颜色。</param>
    /// <returns>PNG。</returns>
    public static byte[] Png(Color color)
    {
        using var image = new Bitmap(100, 50);
        using var graphics = Graphics.FromImage(image);
        using var stream = new MemoryStream();
        graphics.Clear(color);
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    /// <summary>
    /// 构造生成结果，DPI 由独立检查设置。
    /// </summary>
    /// <param name="content">文件内容。</param>
    /// <param name="format">格式。</param>
    /// <param name="zip">是否为 ZIP。</param>
    /// <param name="dpi">生成 DPI。</param>
    /// <returns>生成结果。</returns>
    public static LabelFile File(byte[] content, LabelFormat format, bool zip = false, int dpi = 100) =>
        new(content, zip ? "application/zip" : format == LabelFormat.Pdf ? "application/pdf" : "image/png", "print-check", format)
        {
            Dpi = dpi
        };

    /// <summary>
    /// 构造有固定顺序的 ZIP；不对名字排序，以便核对服务返回的条目顺序。
    /// </summary>
    /// <param name="entries">条目名称和内容。</param>
    /// <returns>ZIP。</returns>
    public static byte[] Zip(params (string Name, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                using var output = zip.CreateEntry(entry.Name).Open();
                output.Write(entry.Content, 0, entry.Content.Length);
            }
        }

        return stream.ToArray();
    }
}
#endif
