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
    /// <summary>
    /// 运行端到端检查；没设置 LABEL_SERVICE_URL 时只打印说明。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
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

        BatchRenderRequest Batch(LabelFormat format)
        {
            var batch = new BatchRenderRequest("BOX_LABEL", format)
                .SetCommon("partName", "电源适配器")
                .SetCommon("partNo", "3100-0123")
                .SetCommon("qty", 50)
                .SetCommon("batchNo", "20261008-A")
                .SetCommon("prodDate", new DateTime(2026, 10, 8));
            batch.AddItem().Set("boxNo", "C2610080001");
            batch.AddItem(copies: 2).Set("boxNo", "C2610080002");
            return batch;
        }

        var merged = await client.RenderBatchAsync(Batch(LabelFormat.Pdf));
        check("端到端：SDK 批量合并成一个 PDF，张数含份数",
            merged is { LabelCount: 3, IsZip: false } && merged.Content.Length > 4 && merged.Content[0] == (byte)'%' && merged.Content[1] == (byte)'P');
        var zipped = await client.RenderBatchAsync(Batch(LabelFormat.Png));
        check("端到端：SDK 批量 PNG 默认打包成 zip",
            zipped is { LabelCount: 3, IsZip: true } && zipped.Content.Length > 4 && zipped.Content[0] == (byte)'P' && zipped.Content[1] == (byte)'K');

        var sheet = new RenderRequest("BOX_LABEL", LabelFormat.Pdf) { Layout = "A4_100x60_2x4", Copies = 8 }
            .Set("partName", "电源适配器").Set("partNo", "3100-0123").Set("qty", 50).Set("batchNo", "20261008-A").Set("boxNo", "C2610080001");
        var onePage = await client.RenderAsync(sheet);
        var pages = Batch(LabelFormat.Pdf);
        pages.Layout = "A4_100x60_2x4";
        pages.StartSlot = 7;
        var twoPages = await client.RenderBatchAsync(pages);
        check("端到端：拼版 PDF，单张 8 份是 1 页；批量 3 张从第 7 格开始是 2 页（PageCount 来自 X-Page-Count）",
            onePage is { LabelCount: 8, PageCount: 1 } && twoPages is { LabelCount: 3, PageCount: 2, IsZip: false });
        sheet.Layout = "A4_NOPE";
        check("端到端：版式不存在映射为 LAYOUT_NOT_FOUND", await Fails(() => client.RenderAsync(sheet)) is { ErrorCode: LabelErrorCodes.LayoutNotFound, StatusCode: 404 });

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

    /// <summary>
    /// 执行一个调用，返回抛出的 <see cref="LabelServiceException"/>。
    /// </summary>
    /// <param name="action">调用。</param>
    /// <returns>抛出的异常；调用成功时为 null。</returns>
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
