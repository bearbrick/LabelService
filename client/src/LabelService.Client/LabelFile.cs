using System.Text;

namespace LabelService.Client;

/// <summary>
/// 生成结果。除内容外的属性都来自响应头。
/// </summary>
public sealed class LabelFile
{
    /// <summary>
    /// 创建结果对象。一般由 <see cref="LabelServiceClient"/> 创建，调用方写单元测试时也可以自己构造。
    /// </summary>
    /// <param name="content">文件内容。</param>
    /// <param name="contentType">Content-Type。</param>
    /// <param name="fileName">建议的文件名。</param>
    /// <param name="format">请求的输出格式。</param>
    public LabelFile(byte[] content, string contentType, string fileName, LabelFormat format)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ContentType = contentType ?? "";
        FileName = fileName ?? "";
        Format = format;
    }

    /// <summary>文件内容。</summary>
    public byte[] Content
    {
        get;
    }

    /// <summary>Content-Type，如 image/png、application/pdf、application/zip。</summary>
    public string ContentType
    {
        get;
    }

    /// <summary>服务端建议的文件名，如 BOX_LABEL_20261008.pdf。</summary>
    public string FileName
    {
        get;
    }

    /// <summary>请求的输出格式。批量 Zip 时内容是 ZIP 包，<see cref="IsZip"/> 为 true。</summary>
    public LabelFormat Format
    {
        get;
    }

    /// <summary>内容是否是 ZIP 包。</summary>
    public bool IsZip => ContentType.StartsWith("application/zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>实际使用的模板版本号（X-Template-Version）。</summary>
    public int? TemplateVersion
    {
        get; internal set;
    }

    /// <summary>文件里的标签张数，含份数（X-Label-Count）。</summary>
    public int LabelCount
    {
        get; internal set;
    }

    /// <summary>实际使用的 DPI（X-Label-Dpi）。</summary>
    public int Dpi
    {
        get; internal set;
    }

    /// <summary>链路 ID（X-Trace-Id），反馈问题时提供给服务方。</summary>
    public string? TraceId
    {
        get; internal set;
    }

    /// <summary>本次调用的 X-Request-Id，由 SDK 生成。</summary>
    public string? RequestId
    {
        get; internal set;
    }

    /// <summary>服务端处理总耗时（Server-Timing 里的 total）。</summary>
    public TimeSpan? ServerDuration
    {
        get; internal set;
    }

    /// <summary>服务端各阶段耗时（毫秒），键是阶段名：auth、tpl、validate、layout、draw、encode、total。</summary>
    public IReadOnlyDictionary<string, double> ServerTiming { get; internal set; } = new Dictionary<string, double>();

    /// <summary>渲染警告（X-Label-Warnings），如 FONT_FALLBACK:SimSun。</summary>
    public IReadOnlyList<string> Warnings { get; internal set; } = new string[0];

    /// <summary>
    /// 保存到文件，目录不存在时自动创建。
    /// </summary>
    /// <param name="path">文件路径。</param>
    public void Save(string path)
    {
        EnsureDirectory(path);
        File.WriteAllBytes(path, Content);
    }

    /// <summary>
    /// 异步保存到文件，目录不存在时自动创建。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示保存操作的任务。</returns>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureDirectory(path);
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await stream.WriteAsync(Content, 0, Content.Length, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 以只读流的方式读取内容。
    /// </summary>
    /// <returns>内存流。</returns>
    public Stream OpenRead() => new MemoryStream(Content, writable: false);

    /// <summary>
    /// 取 ZPL 指令文本。
    /// </summary>
    /// <returns>UTF-8 解码后的 ZPL。</returns>
    /// <exception cref="InvalidOperationException">不是 ZPL 或者是 ZIP 包。</exception>
    public string GetZplText()
    {
        if (Format != LabelFormat.Zpl || IsZip)
        {
            throw new InvalidOperationException("只有 ZPL 格式且不是 ZIP 包时才能取指令文本。");
        }

        return Encoding.UTF8.GetString(Content);
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory != null && directory.Length > 0)
        {
            Directory.CreateDirectory(directory);
        }
    }
}
