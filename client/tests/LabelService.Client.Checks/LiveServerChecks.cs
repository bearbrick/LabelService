namespace LabelService.Client.Checks;

/// <summary>
/// 对真实服务的端到端检查，验证 SDK 和服务端的契约真正对得上。
/// </summary>
/// <remarks>
/// 只在设置了环境变量 LABEL_SERVICE_URL 时运行（根目录 verify.ps1 会临时启动后端服务并设置它）；
/// Key 取 LABEL_SERVICE_API_KEY，默认是开发环境的测试 Key。服务端需要加载示例模板 BOX_LABEL。
/// </remarks>
internal static class LiveServerChecks
{
    public static async Task RunAsync(Action<string, bool> check)
    {
        var url = Environment.GetEnvironmentVariable("LABEL_SERVICE_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            Console.WriteLine("SKIP: 未设置 LABEL_SERVICE_URL，跳过对真实服务的端到端检查（运行根目录 verify.ps1 会自动执行）");
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable("LABEL_SERVICE_API_KEY") ?? "lbl_test_local_dev";
        using var client = new LabelServiceClient(new LabelServiceClientOptions { BaseAddress = new Uri(url), ApiKey = apiKey, MaxRetries = 0 });

        var file = await client.RenderAsync(new RenderRequest("BOX_LABEL", LabelFormat.Png)
            .Set("partName", "电源适配器")
            .Set("partNo", "3100-0123")
            .Set("qty", 50)
            .Set("batchNo", "20261008-A")
            .Set("prodDate", new DateTime(2026, 10, 8))
            .Set("boxNo", "C2610080001"));
        check("端到端：SDK 生成 PNG，响应头解析正确",
            file is { LabelCount: 1, Dpi: 300 } && file.TemplateVersion is not null && file.ServerDuration is not null
            && file.Content.Length > 8 && file.Content[0] == 0x89 && file.Content[1] == 0x50);

        var schema = await client.GetSchemaAsync("BOX_LABEL");
        check("端到端：SDK 读取字段定义", schema.Fields.Count == 6 && schema.Sample.ContainsKey("boxNo"));

        var invalid = await Fails(() => client.RenderAsync(new RenderRequest("BOX_LABEL", LabelFormat.Png).Set("qty", "abc")));
        check("端到端：服务端的错误码和明细原样传给调用方",
            invalid is { ErrorCode: LabelErrorCodes.FieldRequired, StatusCode: 400 }
            && invalid.Errors.Any(error => error.Field == "qty" && error.Code == LabelErrorCodes.FieldTypeInvalid));
        var notFound = await Fails(() => client.RenderAsync(new RenderRequest("NOPE", LabelFormat.Png)));
        check("端到端：模板不存在映射为 TEMPLATE_NOT_FOUND", notFound is { ErrorCode: LabelErrorCodes.TemplateNotFound, StatusCode: 404 });

        using var wrongKey = new LabelServiceClient(new LabelServiceClientOptions { BaseAddress = new Uri(url), ApiKey = "lbl_test_wrong", MaxRetries = 0 });
        var unauthorized = await Fails(() => wrongKey.GetSchemaAsync("BOX_LABEL"));
        check("端到端：Key 无效映射为 UNAUTHORIZED", unauthorized is { ErrorCode: LabelErrorCodes.Unauthorized, StatusCode: 401 });
    }

    private static async Task<LabelServiceException?> Fails(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (LabelServiceException ex)
        {
            return ex;
        }
    }
}
