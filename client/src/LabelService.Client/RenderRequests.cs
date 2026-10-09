namespace LabelService.Client;

/// <summary>
/// 输出格式。
/// </summary>
public enum LabelFormat
{
    /// <summary>PDF，一张标签一页。</summary>
    Pdf,

    /// <summary>PNG 图片。</summary>
    Png,

    /// <summary>ZPL 指令，可直接发给斑马等标签打印机。</summary>
    Zpl,
}

/// <summary>
/// 批量输出方式。
/// </summary>
public enum BatchOutput
{
    /// <summary>合并成一个文件：PDF 多页、ZPL 拼接。</summary>
    Merge,

    /// <summary>每张一个文件打包成 ZIP。PNG 只能用这种方式。</summary>
    Zip,
}

/// <summary>
/// 单张生成请求。
/// </summary>
/// <example>
/// <code>
/// var request = new RenderRequest("BOX_LABEL", LabelFormat.Pdf)
///     .Set("partName", "电源适配器")
///     .Set("prodDate", DateTime.Today);
/// </code>
/// </example>
public sealed class RenderRequest
{
    /// <summary>
    /// 创建请求。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="format">输出格式。</param>
    public RenderRequest(string templateCode, LabelFormat format)
    {
        TemplateCode = templateCode;
        Format = format;
    }

    /// <summary>模板编码。</summary>
    public string TemplateCode
    {
        get; set;
    }

    /// <summary>模板版本号，不设则用最新发布版本。</summary>
    public int? Version
    {
        get; set;
    }

    /// <summary>输出格式。</summary>
    public LabelFormat Format
    {
        get; set;
    }

    /// <summary>203、300 或 600，不设则用模板设置。</summary>
    public int? Dpi
    {
        get; set;
    }

    /// <summary>仅 PNG 有效：输出黑白图。</summary>
    public bool Mono
    {
        get; set;
    }

    /// <summary>份数，1–100。PNG 只能 1 份。</summary>
    public int Copies { get; set; } = 1;

    /// <summary>
    /// 字段数据。值可以是 string、数字、bool、DateTime、DateTimeOffset、byte[]（图片）或枚举；null 不发送。
    /// </summary>
    public IDictionary<string, object?> Data { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// 设置一个字段，可链式调用。
    /// </summary>
    /// <param name="key">字段 key，区分大小写。</param>
    /// <param name="value">字段值。</param>
    /// <returns>当前请求。</returns>
    public RenderRequest Set(string key, object? value)
    {
        Data[key] = value;
        return this;
    }
}

/// <summary>
/// 批量生成请求。公共字段只传一次，每条只传不同的字段。
/// </summary>
public sealed class BatchRenderRequest
{
    /// <summary>
    /// 创建请求。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="format">输出格式。</param>
    public BatchRenderRequest(string templateCode, LabelFormat format)
    {
        TemplateCode = templateCode;
        Format = format;
        Output = format == LabelFormat.Png ? BatchOutput.Zip : BatchOutput.Merge;
    }

    /// <summary>模板编码。</summary>
    public string TemplateCode
    {
        get; set;
    }

    /// <summary>模板版本号，不设则用最新发布版本。</summary>
    public int? Version
    {
        get; set;
    }

    /// <summary>输出格式。</summary>
    public LabelFormat Format
    {
        get; set;
    }

    /// <summary>203、300 或 600，不设则用模板设置。</summary>
    public int? Dpi
    {
        get; set;
    }

    /// <summary>仅 PNG 有效：输出黑白图。</summary>
    public bool Mono
    {
        get; set;
    }

    /// <summary>输出方式。PNG 默认且只能用 Zip，其余默认 Merge。</summary>
    public BatchOutput Output
    {
        get; set;
    }

    /// <summary>所有条目共用的字段，条目里的同名字段覆盖它。</summary>
    public IDictionary<string, object?> Common { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>条目，输出顺序和这里一致。</summary>
    public IList<BatchItem> Items { get; } = new List<BatchItem>();

    /// <summary>
    /// 设置一个公共字段，可链式调用。
    /// </summary>
    /// <param name="key">字段 key。</param>
    /// <param name="value">字段值。</param>
    /// <returns>当前请求。</returns>
    public BatchRenderRequest SetCommon(string key, object? value)
    {
        Common[key] = value;
        return this;
    }

    /// <summary>
    /// 追加一条并返回它，接着用 <see cref="BatchItem.Set"/> 填字段。
    /// </summary>
    /// <param name="copies">份数，1–100。</param>
    /// <returns>新条目。</returns>
    public BatchItem AddItem(int copies = 1)
    {
        var item = new BatchItem { Copies = copies };
        Items.Add(item);
        return item;
    }
}

/// <summary>
/// 批量请求里的一条。
/// </summary>
public sealed class BatchItem
{
    /// <summary>这一条的字段数据。</summary>
    public IDictionary<string, object?> Data { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>份数，1–100。</summary>
    public int Copies { get; set; } = 1;

    /// <summary>
    /// 设置一个字段，可链式调用。
    /// </summary>
    /// <param name="key">字段 key。</param>
    /// <param name="value">字段值。</param>
    /// <returns>当前条目。</returns>
    public BatchItem Set(string key, object? value)
    {
        Data[key] = value;
        return this;
    }
}
