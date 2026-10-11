namespace LabelService.Demo.Mock;

/// <summary>
/// 模拟服务里的两个模板，字段和服务端的示例模板一致（backend/samples/templates）。
/// </summary>
internal static class MockTemplates
{
    /// <summary>
    /// 全部模拟模板。
    /// </summary>
    private static readonly MockTemplate[] All =
    [
        new MockTemplate("BOX_LABEL", "成品箱标", 100, 60, 300,
        [
            new MockField("partName", "品名", "string", true, "电源适配器"),
            new MockField("partNo", "料号", "string", true, "3100-0123"),
            new MockField("qty", "数量", "number", true, "50"),
            new MockField("batchNo", "批次", "string", true, "20261008-A"),
            new MockField("prodDate", "生产日期", "date", false, "2026-10-08"),
            new MockField("boxNo", "箱号", "string", true, "C2610080001"),
        ]),
        new MockTemplate("A4_SHIPPING_MARK", "出口唛头 A4", 210, 297, 300,
        [
            new MockField("consignee", "收货人唛", "string", true, "ACME TRADING"),
            new MockField("destination", "目的港", "string", true, "HAMBURG, GERMANY"),
            new MockField("poNo", "订单号", "string", true, "PO-2026-08817"),
            new MockField("itemNo", "货号", "string", true, "PA-65W-EU"),
            new MockField("qty", "每箱数量", "number", true, "40"),
            new MockField("cartonNo", "箱号", "number", true, "12"),
            new MockField("cartonTotal", "总箱数", "number", true, "80"),
        ]),
    ];

    /// <summary>
    /// 按编码找模板（忽略大小写和首尾空白）。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>模板；没有时为 null。</returns>
    public static MockTemplate? Find(string code) =>
        All.FirstOrDefault(template => string.Equals(template.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// 模拟模板。
/// </summary>
/// <param name="code">模板编码。</param>
/// <param name="name">模板名称。</param>
/// <param name="width">宽度，mm。</param>
/// <param name="height">高度，mm。</param>
/// <param name="dpi">默认分辨率。</param>
/// <param name="fields">字段定义。</param>
internal sealed class MockTemplate(string code, string name, double width, double height, int dpi, MockField[] fields)
{
    /// <summary>
    /// 模板编码。
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// 模板名称。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 宽度，mm。
    /// </summary>
    public double Width { get; } = width;

    /// <summary>
    /// 高度，mm。
    /// </summary>
    public double Height { get; } = height;

    /// <summary>
    /// 默认分辨率。
    /// </summary>
    public int Dpi { get; } = dpi;

    /// <summary>
    /// 版本号（模拟服务固定为 1）。
    /// </summary>
    public int Version => 1;

    /// <summary>
    /// 字段定义。
    /// </summary>
    public MockField[] Fields { get; } = fields;

    /// <summary>
    /// GET /api/v1/templates/{code}/schema 的响应体。
    /// </summary>
    /// <returns>响应体（匿名对象，按驼峰序列化）。</returns>
    public object Schema() => new
    {
        templateCode = Code,
        name = Name,
        version = Version,
        page = new
        {
            width = Width,
            height = Height,
            dpi = Dpi
        },
        fields = Fields.Select(field => new { key = field.Key, name = field.Name, type = field.Type, required = field.Required }),
        sample = Fields.ToDictionary(field => field.Key, field => field.Type == "number" ? (object)decimal.Parse(field.Sample, System.Globalization.CultureInfo.InvariantCulture) : field.Sample),
    };

    /// <summary>
    /// 缺少的必填字段，按服务端的错误明细格式（index、field、code、message）。
    /// </summary>
    /// <param name="data">一条标签的字段值（文本）。</param>
    /// <param name="index">批量里的序号（从 0 开始）；单张时为 null，明细里不写 index。</param>
    /// <returns>每个缺少的字段一条明细；不缺时为空。</returns>
    public List<Dictionary<string, object>> Missing(Dictionary<string, string> data, int? index)
    {
        var errors = new List<Dictionary<string, object>>();
        foreach (var field in Fields.Where(field => field.Required && (!data.TryGetValue(field.Key, out var value) || string.IsNullOrWhiteSpace(value) || value == "null")))
        {
            var error = new Dictionary<string, object> { ["field"] = field.Key, ["code"] = "FIELD_REQUIRED", ["message"] = "缺少必填字段 " + field.Key };
            if (index is { } i)
            {
                error["index"] = i;
            }

            errors.Add(error);
        }

        return errors;
    }
}

/// <summary>
/// 模拟字段。
/// </summary>
/// <param name="key">字段 key。</param>
/// <param name="name">字段名称。</param>
/// <param name="type">字段类型：string、number、date。</param>
/// <param name="required">是否必填。</param>
/// <param name="sample">样例值（文本）。</param>
internal sealed class MockField(string key, string name, string type, bool required, string sample)
{
    /// <summary>
    /// 字段 key。
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// 字段名称。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 字段类型。
    /// </summary>
    public string Type { get; } = type;

    /// <summary>
    /// 是否必填。
    /// </summary>
    public bool Required { get; } = required;

    /// <summary>
    /// 样例值。
    /// </summary>
    public string Sample { get; } = sample;
}
