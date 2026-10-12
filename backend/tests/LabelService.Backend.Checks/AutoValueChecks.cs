using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LabelService.Application.Rendering;
using LabelService.Application.Templates;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LabelService.Backend.Checks;

/// <summary>
/// 字段自动值（ADR 0012）：七种写法按类型算值、调用方传值优先、时区换算、发布前检查、字段定义接口和真实调用。
/// </summary>
internal static class AutoValueChecks
{
    /// <summary>
    /// 检查里用的模板编码。
    /// </summary>
    private const string Code = "AUTO_VALUE_CHECK";

    /// <summary>
    /// 运行自动值的全部检查：规则和时钟不需要服务；接口检查用临时数据目录启动服务。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        CheckRules(check);
        CheckClock(check);
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory);
            using var http = factory.CreateClient();
            await CheckServerAsync(factory.Services, http, check);
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    /// <summary>
    /// 带自动值的字段：日期四种、数字两种、文本两种，还有一个同时有固定默认值的。
    /// </summary>
    /// <returns>字段定义。</returns>
    private static FieldDefinition[] Fields() =>
    [
        new() { Key = "shipDate", Type = FieldType.Date, Auto = AutoValues.Today },
        new() { Key = "printedAt", Type = FieldType.Date, Auto = AutoValues.Now },
        new() { Key = "expireDate", Type = FieldType.Date, Auto = "today+30d" },
        new() { Key = "nextMonth", Type = FieldType.Date, Auto = "today+1m" },
        new() { Key = "lastYear", Type = FieldType.Date, Auto = "today-1y" },
        new() { Key = "boxSeq", Type = FieldType.Number, Auto = AutoValues.Index },
        new() { Key = "boxTotal", Type = FieldType.Number, Auto = AutoValues.Total },
        new() { Key = "system", Auto = AutoValues.Client },
        new() { Key = "traceNo", Auto = AutoValues.RequestId },
        new() { Key = "line", Auto = AutoValues.Client, Default = JsonDocument.Parse("\"A1\"").RootElement },
    ];

    /// <summary>
    /// 规则：按类型算值、月底取最后一天、批量序号、调用方传值优先、写法不认识时当作没有自动值。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    private static void CheckRules(Action<string, bool> check)
    {
        var context = new AutoValueContext { Now = new DateTime(2026, 1, 31, 10, 20, 30, 567), Total = 20, ClientName = "MES", RequestId = "REQ-1" };
        var result = FieldValidator.Validate(Fields(), null, Fixtures.Data("{}"), 2, context);
        var v = result.Values;
        check("自动值：必填字段没传也通过（自动值补上）", result.IsValid);
        check("自动值：today 是当天 0 点，now 精确到秒",
            v["shipDate"] is DateTime ship && ship == new DateTime(2026, 1, 31) && v["printedAt"] is DateTime at && at == new DateTime(2026, 1, 31, 10, 20, 30));
        check("自动值：today+30d、today-1y；1 月 31 日加 1 个月是 2 月 28 日",
            v["expireDate"] is DateTime expire && expire == new DateTime(2026, 3, 2)
            && v["lastYear"] is DateTime last && last == new DateTime(2025, 1, 31)
            && v["nextMonth"] is DateTime next && next == new DateTime(2026, 2, 28));
        check("自动值：index 是条目序号加 1，total 是总条数（都是 decimal）", v["boxSeq"] is 3m && v["boxTotal"] is 20m);
        check("自动值：client 是调用方名称，requestId 是请求号；自动值优先于固定默认值",
            v["system"] as string == "MES" && v["traceNo"] as string == "REQ-1" && v["line"] as string == "MES");

        var passed = FieldValidator.Validate(Fields(), null, Fixtures.Data("""{"shipDate":"2026-05-01","boxSeq":7}"""), 2, context);
        check("自动值：调用方传了值就用传的", passed.Values["shipDate"] is DateTime given && given == new DateTime(2026, 5, 1) && passed.Values["boxSeq"] is 7m);

        var wrong = FieldValidator.Validate([new FieldDefinition { Key = "qty", Type = FieldType.Number, Auto = AutoValues.Today }], null, Fixtures.Data("{}"), null, context);
        check("自动值：写法和类型不匹配时当作没有自动值，必填字段照样报 FIELD_REQUIRED", wrong.Errors is [{ Field: "qty", Code: ErrorCodes.FieldRequired }]);

        var preview = FieldValidator.Validate([new FieldDefinition { Key = "day", Type = FieldType.Date, Auto = AutoValues.Today }], null, Fixtures.Data("{}"));
        check("自动值：不传上下文时按本机当前时间算（预览、试渲染）", preview.Values["day"] is DateTime today && today == DateTime.Now.Date);

        string[] valid = ["today", "now", "today+1d", "today-9999d", "today+6m", "today+1y"];
        string[] invalid = ["Today", "today+0d", "today+10000d", "today+30", "today30d", "today+-1d", "today+1w", "yesterday", ""];
        check("自动值写法：date 认 today、now、today±N(d|m|y)，N 为 1～9999，区分大小写",
            valid.All(auto => AutoValues.IsValid(FieldType.Date, auto)) && !invalid.Any(auto => AutoValues.IsValid(FieldType.Date, auto)));
        check("自动值写法：number 只认 index、total，string 只认 client、requestId，image 没有自动值",
            AutoValues.IsValid(FieldType.Number, "index") && AutoValues.IsValid(FieldType.Number, "total") && !AutoValues.IsValid(FieldType.Number, "today")
            && AutoValues.IsValid(FieldType.String, "client") && AutoValues.IsValid(FieldType.String, "requestId") && !AutoValues.IsValid(FieldType.String, "index")
            && !AutoValues.IsValid(FieldType.Image, "today"));

        var template = Fixtures.LoadTemplate(Fixtures.BackendRoot());
        template.Fields.Add(new FieldDefinition { Key = "seq", Type = FieldType.Number, Auto = "today" });
        check("发布前检查拦下和类型不匹配的自动值", TemplateChecker.PreviewErrors(template).Any(error => error.Contains("seq", StringComparison.Ordinal) && error.Contains("自动值", StringComparison.Ordinal)));
    }

    /// <summary>
    /// 时钟：按配置的时区换算当前时间；时区不存在时改用本机时区；模板没有自动值字段时不建上下文。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    private static void CheckClock(Action<string, bool> check)
    {
        AutoValueClock Clock(string? zone) => new(new FixedTime(new DateTimeOffset(2026, 1, 31, 18, 0, 0, TimeSpan.Zero)), Options.Create(new RenderOptions { TimeZone = zone }), NullLogger<AutoValueClock>.Instance);
        check("自动值时区：UTC 18:00 在 Asia/Shanghai 是第二天 2 点，在 UTC 还是当天",
            Clock("Asia/Shanghai").Now() == new DateTime(2026, 2, 1, 2, 0, 0) && Clock("UTC").Now() == new DateTime(2026, 1, 31, 18, 0, 0));
        check("自动值时区：配置的时区不存在时改用本机时区，不报错", Clock("Mars/Olympus").TimeZone == TimeZoneInfo.Local && Clock(null).TimeZone == TimeZoneInfo.Local);
        var fields = Fields();
        check("自动值时钟：模板没有自动值字段时不建上下文（热路径不多做事）",
            Clock(null).For([new FieldDefinition { Key = "a" }], 1, "x", "y") is null
            && Clock("UTC").For(fields, 3, "MES", "R") is { Total: 3, ClientName: "MES", RequestId: "R" } context && context.Now == new DateTime(2026, 1, 31, 18, 0, 0));
    }

    /// <summary>
    /// 接口：字段定义返回 auto、样例里没有自动值字段；真实调用不传这些字段也能生成；请求号来自 X-Request-Id。
    /// </summary>
    /// <param name="services">服务的根容器。</param>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    private static async Task CheckServerAsync(IServiceProvider services, HttpClient http, Action<string, bool> check)
    {
        var template = Fixtures.LoadTemplate(Fixtures.BackendRoot());
        template.Code = Code;
        var prodDate = template.Fields.Single(field => field.Key == "prodDate");
        prodDate.Auto = AutoValues.Today;
        prodDate.Required = true;
        template.Fields.Add(new FieldDefinition { Key = "boxSeq", Type = FieldType.Number, Auto = AutoValues.Index, Sample = JsonDocument.Parse("1").RootElement });
        var manager = new ScopedTemplateManager(services);
        await manager.SaveDraftAsync(template, "checks");
        await manager.PublishAsync(Code, "checks");

        using var schemaRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/templates/{Code}/schema");
        schemaRequest.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        var schema = await (await http.SendAsync(schemaRequest)).Content.ReadFromJsonAsync<TemplateSchemaResponse>(LabelJson.Options);
        check("字段定义接口返回 auto，样例里不放有自动值的字段（照着样例传会盖掉自动值）",
            schema?.Fields.Single(field => field.Key == "prodDate").Auto == AutoValues.Today
            && schema.Fields.Single(field => field.Key == "boxSeq").Auto == AutoValues.Index
            && !schema.Sample.ContainsKey("prodDate") && !schema.Sample.ContainsKey("boxSeq") && schema.Sample.ContainsKey("boxNo"));

        // 数据里不带 prodDate（必填，但有自动值）和 boxSeq
        var data = Fixtures.Data(Fixtures.BoxLabelData);
        data.Remove("prodDate");
        var body = JsonSerializer.Serialize(new
        {
            templateCode = Code,
            format = "png",
            data
        }, LabelJson.Options);
        using var render = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        render.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        render.Headers.Add(HeaderNames.RequestId, "AUTO-REQ-1");
        using var rendered = await http.SendAsync(render);
        check("真实调用：必填字段有自动值时不传也能生成", rendered.IsSuccessStatusCode);
    }

    /// <summary>
    /// 固定时间的时间来源。
    /// </summary>
    /// <param name="now">固定的 UTC 时间。</param>
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => now;
    }
}
