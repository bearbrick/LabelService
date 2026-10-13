using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LabelService.Application.Templates;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Validation;

namespace LabelService.Backend.Checks;

/// <summary>
/// 显式字段来源、互斥配置、固定值零、旧回退语义及真实接口检查。
/// </summary>
internal static class FieldSourceChecks
{
    /// <summary>
    /// 运行纯规则、发布约束及独立服务的端到端检查。
    /// </summary>
    /// <param name="root">后端目录。</param>
    /// <param name="check">记录结果。</param>
    /// <returns>检查完成时结束。</returns>
    public static async Task RunAsync(string root, Action<string, bool> check)
    {
        var fields = new FieldDefinition[]
        {
            new() { Key = "fixed", Type = FieldType.Number, Source = FieldSource.Template, Default = JsonSerializer.SerializeToElement(0) },
            new() { Key = "time", Type = FieldType.Date, Source = FieldSource.Template, Auto = "now" },
            new() { Key = "input", Source = FieldSource.Input },
            new() { Key = "blank", Source = FieldSource.Template },
            new() { Key = "old", Default = JsonSerializer.SerializeToElement("fallback") },
        };
        var context = new AutoValueContext { Now = new DateTime(2026, 10, 13, 10, 20, 30) };
        var result = FieldValidator.Validate(fields, Fixtures.Data("""{"fixed":88,"input":"common","old":"common"}"""),
            Fixtures.Data("""{"fixed":{"ignored":true},"time":"invalid","input":"data","old":"data"}"""), auto: context);
        check("模板提供忽略条目和公共数据，手写零与当前时间正常取值", result.IsValid && result.Values["fixed"] is 0m
            && result.Values["time"] is DateTime time && time == context.Now);
        check("接口来源取条目优先，旧字段保留回退规则，模板空值不报必填", result.Values["input"] as string == "data"
            && result.Values["old"] as string == "data" && result.Values["blank"] == null);
        fields[2].Default = JsonSerializer.SerializeToElement("must-ignore");
        result = FieldValidator.Validate(fields, null, null, index: 3, auto: context);
        check("接口来源不使用手写回退，错误按批量条目序号报告", result.Errors.Any(error => error.Field == "input"
            && error.Code == ErrorCodes.FieldRequired && error.Index == 3) && result.Values["old"] as string == "fallback");
        var samples = TemplateSampleData.Create(fields, new List<string>());
        check("模板提供完全排除试渲染输入，旧必填默认值仍保持回退", !samples.ContainsKey("fixed") && !samples.ContainsKey("time"));

        var checker = new TemplateChecker(Fixtures.Renderer(root));
        var template = Fixtures.LoadTemplate(root);
        var qty = template.Fields.Single(field => field.Key == "qty");
        qty.Source = FieldSource.Template;
        qty.Default = JsonSerializer.SerializeToElement(0);
        qty.Auto = "total";
        check("发布拒绝同时设置预设与手写值", checker.Check(template).Errors.Any(error => error.Contains("不能同时")));
        qty.Source = FieldSource.Input;
        check("发布拒绝接口来源携带回退配置", checker.Check(template).Errors.Any(error => error.Contains("接口来源")));
        qty.Source = FieldSource.Template;
        qty.Auto = null;
        qty.Default = JsonSerializer.SerializeToElement("不是数字");
        check("手写值仍按类型校验，不被样例值遮盖", checker.Check(template).Errors.Count > 0);

        var directory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(root, directory);
            using var http = factory.CreateClient();
            template.Code = "FIELD_SOURCE_CHECK";
            qty.Default = JsonSerializer.SerializeToElement(0);
            var manager = new ScopedTemplateManager(factory.Services);
            await manager.SaveDraftAsync(template, "checks");
            await manager.PublishAsync(template.Code, "checks");
            using var schemaRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/templates/{template.Code}/schema");
            schemaRequest.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
            using var response = await http.SendAsync(schemaRequest);
            var schema = await response.Content.ReadFromJsonAsync<TemplateSchemaResponse>(LabelJson.Options);
            check("schema 返回来源且不输出模板提供的样例值", schema?.Fields.Single(field => field.Key == "qty")
                is { Source: FieldSource.Template, Required: false } && !schema.Sample.ContainsKey("qty"));
            var data = Fixtures.Data(Fixtures.BoxLabelData);
            using var first = await RenderAsync(http, template.Code, data);
            data["qty"] = JsonSerializer.SerializeToElement(999);
            using var overrideResult = await RenderAsync(http, template.Code, data);
            data.Remove("qty");
            using var missingResult = await RenderAsync(http, template.Code, data);
            var firstContent = await first.Content.ReadAsByteArrayAsync();
            var overrideContent = await overrideResult.Content.ReadAsByteArrayAsync();
            var missingContent = await missingResult.Content.ReadAsByteArrayAsync();
            check("真实接口传入和省略模板字段，生成内容完全相同", first.IsSuccessStatusCode && overrideResult.IsSuccessStatusCode
                && missingResult.IsSuccessStatusCode && firstContent.SequenceEqual(overrideContent) && overrideContent.SequenceEqual(missingContent));
        }
        finally { TempDataDirectory.Delete(directory); }
    }

    /// <summary>
    /// 通过开放接口生成检查模板，不使用管理令牌。
    /// </summary>
    /// <param name="http">独立服务客户端。</param>
    /// <param name="code">模板编码。</param>
    /// <param name="data">业务数据。</param>
    /// <returns>响应，由调用方释放。</returns>
    private static async Task<HttpResponseMessage> RenderAsync(HttpClient http, string code, Dictionary<string, JsonElement> data)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { templateCode = code, format = "png", data }), Encoding.UTF8, "application/json")
        };
        request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        return await http.SendAsync(request);
    }
}
