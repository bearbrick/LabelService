using System.Net;
using System.Text;
using System.Text.Json;
using LabelService.Contracts.Protocol;
using LabelService.Server.Auth;
using SkiaSharp;

namespace LabelService.Backend.Checks;

/// <summary>
/// 管理接口（T-014）：业务分组、模板的全流程（新建、草稿、发布、版本、回滚、换分组、删除）和业务范围、调用方的业务授权和 IP 白名单、
/// 画布预设、素材上传（类型判断、SVG 清洗）、调用日志和操作日志、概览、接口调试。
/// </summary>
internal static class AdminResourceChecks
{
    public static async Task RunAsync(HttpClient http, AdminSession admin, Action<string, bool> check)
    {
        var pack = await admin.JsonAsync(HttpMethod.Post, "/api/admin/businesses", new
        {
            code = "pack",
            name = "成品包装",
            description = "成品箱标",
            color = "#00939A"
        });
        var packId = pack.GetProperty("id").GetInt32();
        check("新建业务分组（编码转大写），编码重复拒绝",
            pack.GetProperty("code").GetString() == "PACK"
            && await AdminSession.CodeAsync(await admin.PostAsync("/api/admin/businesses", new
            {
                code = "PACK",
                name = "重复"
            })) == "INVALID");
        var businesses = await admin.JsonAsync(HttpMethod.Get, "/api/admin/businesses");
        var sampleId = businesses.EnumerateArray().Single(item => item.GetProperty("code").GetString() == "SAMPLE").GetProperty("id").GetInt32();

        var (designer, _) = await AdminAccountChecks.CreateUserAsync(http, admin, "lifang", "模板设计员", false, [packId]);
        var (viewer, _) = await AdminAccountChecks.CreateUserAsync(http, admin, "zhaomin", "只读访客", true, []);

        await CheckTemplatesAsync(http, admin, designer, packId, sampleId, check);
        await CheckClientsAsync(http, admin, packId, check);
        await CheckResourcesAsync(http, admin, designer, viewer, check);
        await CheckLogsAsync(admin, designer, viewer, check);
    }

    private static async Task CheckTemplatesAsync(HttpClient http, AdminSession admin, AdminSession designer, int packId, int sampleId, Action<string, bool> check)
    {
        check("设计员不能在自己范围以外的业务分组里新建模板",
            await AdminSession.CodeAsync(await designer.PostAsync("/api/admin/templates", new
            {
                code = "OUT_LABEL",
                name = "越界",
                businessId = sampleId
            })) == "FORBIDDEN");
        var created = await designer.PostAsync("/api/admin/templates", new
        {
            code = "CARTON_LABEL",
            name = "纸箱标",
            businessId = packId,
            width = 80,
            height = 50,
            dpi = 203
        });
        var opened = await designer.JsonAsync(HttpMethod.Get, "/api/admin/templates/CARTON_LABEL");
        check("新建模板得到空白草稿（尺寸按请求），状态是从未发布",
            created.IsSuccessStatusCode && opened.GetProperty("hasDraft").GetBoolean() && opened.GetProperty("latestVersion").GetInt32() == 0
            && opened.GetProperty("content").GetProperty("page").GetProperty("width").GetDouble() == 80);
        check("编码重复不能新建", await AdminSession.CodeAsync(await designer.PostAsync("/api/admin/templates", new
        {
            code = "CARTON_LABEL",
            businessId = packId
        })) == "INVALID");

        var content = Template("CARTON_LABEL", "纸箱标", "批次 {{lot}}");
        var saved = await designer.PutAsync("/api/admin/templates/CARTON_LABEL/draft", content);
        check("保存草稿", saved.IsSuccessStatusCode && (await AdminSession.ReadAsync(saved)).GetProperty("draftVersion").GetInt32() == 1);
        var broken = Template("CARTON_LABEL", "纸箱标", "批次 {{nope}}");
        await designer.PutAsync("/api/admin/templates/CARTON_LABEL/draft", broken);
        var refused = await designer.PostAsync("/api/admin/templates/CARTON_LABEL/publish", new
        {
            note = "首版"
        });
        check("发布前检查不通过时返回 400 和逐条问题", await AdminSession.CodeAsync(refused) == "INVALID"
            && (await AdminSession.ReadAsync(refused)).GetProperty("details").EnumerateArray().Any(item => item.GetString()!.Contains("nope")));
        await designer.PutAsync("/api/admin/templates/CARTON_LABEL/draft", content);
        var published = await designer.JsonAsync(HttpMethod.Post, "/api/admin/templates/CARTON_LABEL/publish", new
        {
            note = "首版"
        });
        check("发布得到版本 1 和试渲染耗时", published.GetProperty("version").GetInt32() == 1 && published.GetProperty("renderMs").GetDouble() > 0);
        check("发布后开放接口立即能用（开发 Key 可调用全部模板）", await RenderStatus(http, Fixtures.DevApiKey, "CARTON_LABEL") == 200);

        var item = (await designer.JsonAsync(HttpMethod.Get, "/api/admin/templates")).EnumerateArray().Single(template => template.GetProperty("code").GetString() == "CARTON_LABEL");
        var thumbnail = await designer.GetAsync("/api/admin/templates/CARTON_LABEL/thumbnail");
        using (var image = SKBitmap.Decode(await thumbnail.Content.ReadAsByteArrayAsync()))
        {
            check("模板库列表带画布尺寸、DPI、字段和元素个数；缩略图是约 360 像素宽的 PNG",
                item.GetProperty("width").GetDouble() == 80 && item.GetProperty("dpi").GetInt32() == 203
                && item.GetProperty("fieldCount").GetInt32() == 1 && item.GetProperty("elementCount").GetInt32() == 1
                && thumbnail.Content.Headers.ContentType?.MediaType == "image/png" && image is { Width: > 340 and < 380 });
        }

        var sampleThumbnail = await admin.GetAsync("/api/admin/templates/BOX_LABEL/thumbnail");
        var sampleBytes = await sampleThumbnail.Content.ReadAsByteArrayAsync();
        using (var image = sampleBytes.Length > 0 ? SKBitmap.Decode(sampleBytes) : null)
        {
            check("带条码的示例模板也有缩略图（按模板 DPI 渲染再缩小，条码模块不会不足 1 个打印点）",
                sampleThumbnail.StatusCode == HttpStatusCode.OK && image is { Width: > 340 and < 380 });
        }

        var versions = await designer.JsonAsync(HttpMethod.Get, "/api/admin/templates/CARTON_LABEL/versions");
        check("版本列表带发布说明和发布人", versions.EnumerateArray().First().GetProperty("note").GetString() == "首版"
            && versions.EnumerateArray().First().GetProperty("publishedBy").GetString()!.Contains("lifang"));
        var restored = await designer.JsonAsync(HttpMethod.Post, "/api/admin/templates/CARTON_LABEL/versions/1/restore");
        check("回滚：把 v1 复制成草稿（版本号 2），放弃草稿", restored.GetProperty("draftVersion").GetInt32() == 2
            && (await designer.DeleteAsync("/api/admin/templates/CARTON_LABEL/draft")).IsSuccessStatusCode);

        var visible = (await designer.JsonAsync(HttpMethod.Get, "/api/admin/templates")).EnumerateArray().Select(item => item.GetProperty("code").GetString()).ToArray();
        check("设计员只看到自己业务范围里的模板，范围外的当作不存在（404）",
            visible.SequenceEqual(["CARTON_LABEL"]) && (int)(await designer.GetAsync("/api/admin/templates/BOX_LABEL")).StatusCode == 404);
        check("管理员（全部范围）看到全部模板",
            (await admin.JsonAsync(HttpMethod.Get, "/api/admin/templates")).EnumerateArray().Select(item => item.GetProperty("code").GetString()).Contains("BOX_LABEL"));

        var check1 = await designer.JsonAsync(HttpMethod.Post, "/api/admin/templates/check", broken);
        check("发布前检查接口不保存，只返回问题", check1.GetProperty("errors").GetArrayLength() > 0 && check1.GetProperty("canPublish").GetBoolean() == false);
    }

    private static async Task CheckClientsAsync(HttpClient http, AdminSession admin, int packId, Action<string, bool> check)
    {
        var created = await admin.JsonAsync(HttpMethod.Post, "/api/admin/clients", new
        {
            name = "MES 生产执行系统",
            appId = "mes-prod",
            description = "车间",
            allBusinesses = false,
            businessIds = new[] { packId },
            ipAllowlist = ""
        });
        var key = created.GetProperty("apiKey").GetString()!;
        var clientId = created.GetProperty("client").GetProperty("id").GetInt32();
        var listed = await admin.JsonAsync(HttpMethod.Get, "/api/admin/clients");
        check("新建调用方只返回一次 Key，列表里只有 Key 前缀，没有哈希",
            key.StartsWith("lbl_test_") && listed.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == clientId).GetProperty("keyPrefix").GetString() == key[..13]
            && !listed.ToString().Contains("apiKeyHash", StringComparison.OrdinalIgnoreCase));
        check("调用方按业务分组授权：分组里的模板可以调，别的分组的模板 403",
            await RenderStatus(http, key, "CARTON_LABEL") == 200 && await RenderStatus(http, key, "BOX_LABEL") == 403);

        var sampleId = (await admin.JsonAsync(HttpMethod.Get, "/api/admin/businesses")).EnumerateArray().Single(item => item.GetProperty("code").GetString() == "SAMPLE").GetProperty("id").GetInt32();
        await admin.PutAsync("/api/admin/templates/CARTON_LABEL/business", new
        {
            businessId = sampleId
        });
        check("模板换分组后调用方权限立即按新分组判断", await RenderStatus(http, key, "CARTON_LABEL") == 403);
        await admin.PutAsync("/api/admin/templates/CARTON_LABEL/business", new
        {
            businessId = packId
        });

        await admin.PutAsync($"/api/admin/clients/{clientId}", new
        {
            name = "MES 生产执行系统",
            appId = "mes-prod",
            description = "车间",
            allBusinesses = false,
            businessIds = new[] { packId },
            ipAllowlist = "10.20.1.0/24"
        });
        check("设了 IP 白名单后，来源 IP 不在白名单里返回 401", await RenderStatus(http, key, "CARTON_LABEL") == 401);
        check("IP 白名单格式不对拒绝保存",
            await AdminSession.CodeAsync(await admin.PutAsync($"/api/admin/clients/{clientId}", new
            {
                name = "MES 生产执行系统",
                allBusinesses = true,
                ipAllowlist = "10.20.1.0/99"
            })) == "INVALID");
        var networks = ApiKeyAuthenticator.ParseNetworks("10.20.1.0/24, 192.168.1.5");
        var client = new ApiClient(1, "x", true, new HashSet<int>(), new HashSet<string>(), networks);
        check("IP 白名单匹配网段、单个地址、IPv4 映射的 IPv6 地址",
            client.AllowsIp(IPAddress.Parse("10.20.1.15")) && client.AllowsIp(IPAddress.Parse("192.168.1.5")) && client.AllowsIp(IPAddress.Parse("::ffff:10.20.1.9"))
            && !client.AllowsIp(IPAddress.Parse("10.20.2.1")) && !client.AllowsIp(null));

        await admin.PutAsync($"/api/admin/clients/{clientId}", new
        {
            name = "MES 生产执行系统",
            appId = "mes-prod",
            allBusinesses = false,
            businessIds = new[] { packId },
            ipAllowlist = ""
        });
        var reset = await admin.JsonAsync(HttpMethod.Post, $"/api/admin/clients/{clientId}/reset-key");
        var newKey = reset.GetProperty("apiKey").GetString()!;
        check("重置 Key：旧 Key 立即 401，新 Key 可用", await RenderStatus(http, key, "CARTON_LABEL") == 401 && await RenderStatus(http, newKey, "CARTON_LABEL") == 200);
        await admin.PostAsync($"/api/admin/clients/{clientId}/status", new
        {
            enabled = false
        });
        check("停用调用方立即 401", await RenderStatus(http, newKey, "CARTON_LABEL") == 401);

        await admin.DeleteAsync("/api/admin/templates/CARTON_LABEL");
        check("删除模板后开放接口 404，列表里也没有了",
            await RenderStatus(http, Fixtures.DevApiKey, "CARTON_LABEL") == 404
            && !(await admin.JsonAsync(HttpMethod.Get, "/api/admin/templates")).EnumerateArray().Any(item => item.GetProperty("code").GetString() == "CARTON_LABEL"));
        check("删除的模板编码不能再用",
            await AdminSession.CodeAsync(await admin.PostAsync("/api/admin/templates", new
            {
                code = "CARTON_LABEL",
                businessId = packId
            })) == "INVALID");
    }

    private static async Task CheckResourcesAsync(HttpClient http, AdminSession admin, AdminSession designer, AdminSession viewer, Action<string, bool> check)
    {
        var presets = await viewer.JsonAsync(HttpMethod.Get, "/api/admin/presets");
        check("画布预设：登录就能看（8 个默认，A4 是办公纸张），只读访客不能改",
            presets.GetArrayLength() == 8 && presets[0].GetProperty("kind").GetString() == "办公纸张"
            && await AdminSession.CodeAsync(await viewer.PostAsync("/api/admin/presets", new
            {
                name = "x",
                width = 10,
                height = 10
            })) == "FORBIDDEN");
        var preset = await designer.JsonAsync(HttpMethod.Post, "/api/admin/presets", new
        {
            name = "70×50",
            kind = "标签纸",
            width = 70,
            height = 50
        });
        var presetId = preset.GetProperty("id").GetInt32();
        check("新建、修改、删除画布预设（res.manage）",
            (await designer.PutAsync($"/api/admin/presets/{presetId}", new
            {
                name = "70×40",
                width = 70,
                height = 40
            })).IsSuccessStatusCode
            && (await designer.DeleteAsync($"/api/admin/presets/{presetId}")).IsSuccessStatusCode
            && await AdminSession.CodeAsync(await designer.PostAsync("/api/admin/presets", new
            {
                name = "x",
                width = 0,
                height = 10
            })) == "INVALID");

        var uploaded = await AdminSession.ReadAsync(await designer.UploadAsync("/api/admin/assets", "红色方块.png", Png(), "image/png"));
        var key = uploaded.GetProperty("key").GetString()!;
        var template = Template("ASSET_LABEL", "素材标签", "x");
        template["elements"] = new object[] { new { id = "img", type = "image", x = 2, y = 2, width = 20, height = 20, src = "asset:" + key, fit = "stretch" } };
        await admin.PostAsync("/api/admin/templates", new
        {
            code = "ASSET_LABEL",
            name = "素材标签",
            businessId = (int?)null,
            content = template
        });
        var publish = await admin.PostAsync("/api/admin/templates/ASSET_LABEL/publish", new
        {
            note = ""
        });
        check("上传 PNG 素材后，模板里 asset:Key 立即可用（发布试渲染通过、开放接口出图）",
            key.StartsWith('a') && uploaded.GetProperty("name").GetString() == "红色方块" && publish.IsSuccessStatusCode && await RenderStatus(http, Fixtures.DevApiKey, "ASSET_LABEL") == 200);
        var file = await designer.GetAsync($"/api/admin/assets/{key}/file");
        check("素材文件可以下载给设计器显示", file.IsSuccessStatusCode && file.Content.Headers.ContentType?.MediaType == "image/png");

        var svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10" onload="alert(1)"><script>alert(2)</script><rect width="10" height="10"/></svg>""";
        var svgAsset = await AdminSession.ReadAsync(await designer.UploadAsync("/api/admin/assets", "logo.svg", Encoding.UTF8.GetBytes(svg), "image/svg+xml"));
        var svgText = await (await designer.GetAsync($"/api/admin/assets/{svgAsset.GetProperty("key").GetString()}/file")).Content.ReadAsStringAsync();
        check("上传 SVG 时清洗：脚本和事件属性被去掉", svgText.Contains("<rect", StringComparison.Ordinal) && !svgText.Contains("script", StringComparison.Ordinal) && !svgText.Contains("onload", StringComparison.Ordinal));
        check("不是图片、超过 1 MB 都拒绝",
            await AdminSession.CodeAsync(await designer.UploadAsync("/api/admin/assets", "a.txt", Encoding.UTF8.GetBytes("hello"), "text/plain")) == "INVALID"
            && await AdminSession.CodeAsync(await designer.UploadAsync("/api/admin/assets", "big.png", new byte[RenderLimits.MaxImageBytes + 1], "image/png")) == "INVALID");
        check("素材改名；只读访客不能上传",
            (await designer.PutAsync($"/api/admin/assets/{key}", new
            {
                name = "红方块"
            })).IsSuccessStatusCode
            && await AdminSession.CodeAsync(await viewer.UploadAsync("/api/admin/assets", "x.png", Png(), "image/png")) == "FORBIDDEN");
        var listed = await viewer.JsonAsync(HttpMethod.Get, "/api/admin/assets");
        check("素材列表含内置素材和上传的素材",
            listed.EnumerateArray().Any(item => item.GetProperty("key").GetString() == "logo" && item.GetProperty("builtin").GetBoolean())
            && listed.EnumerateArray().Any(item => item.GetProperty("key").GetString() == key && item.GetProperty("name").GetString() == "红方块"));
        await designer.DeleteAsync($"/api/admin/assets/{key}");
        check("删除素材后，引用它的模板生成时报 IMAGE_INVALID", await RenderCode(http, "ASSET_LABEL") == ErrorCodes.ImageInvalid);

        var fonts = await viewer.JsonAsync(HttpMethod.Get, "/api/admin/fonts");
        check("字体列表", fonts.GetProperty("families").GetArrayLength() > 0);
    }

    private static async Task CheckLogsAsync(AdminSession admin, AdminSession designer, AdminSession viewer, Action<string, bool> check)
    {
        JsonElement logs = default;
        for (var i = 0; i < 100; i++)
        {
            logs = await admin.JsonAsync(HttpMethod.Get, "/api/admin/call-logs?pageSize=200");
            if (logs.EnumerateArray().Any(item => item.GetProperty("templateCode").GetString() == "ASSET_LABEL" && item.GetProperty("status").GetString() == "error"))
            {
                break;
            }

            await Task.Delay(50);
        }

        var failed = logs.EnumerateArray().FirstOrDefault(item => item.GetProperty("templateCode").GetString() == "ASSET_LABEL" && item.GetProperty("status").GetString() == "error");
        check("调用日志：带链路 ID、错误码和错误说明",
            failed.ValueKind == JsonValueKind.Object && failed.GetProperty("traceId").GetString()!.Length > 0
            && failed.GetProperty("errorCode").GetString() == ErrorCodes.ImageInvalid && failed.GetProperty("errorMessage").GetString()!.Contains("不存在"));
        var traced = await admin.JsonAsync(HttpMethod.Get, "/api/admin/call-logs?q=" + Uri.EscapeDataString(failed.GetProperty("traceId").GetString()!));
        check("按链路 ID 查调用日志", traced.GetArrayLength() == 1);
        var mine = await designer.JsonAsync(HttpMethod.Get, "/api/admin/call-logs?pageSize=200");
        check("设计员只看到自己业务范围里的模板的调用日志（管理员能看到的 BOX_LABEL、ASSET_LABEL 他看不到）",
            logs.EnumerateArray().Any(item => item.GetProperty("templateCode").GetString() is "BOX_LABEL" or "ASSET_LABEL")
            && mine.EnumerateArray().All(item => item.GetProperty("templateCode").GetString() is "CARTON_LABEL"));

        var dashboard = await viewer.JsonAsync(HttpMethod.Get, "/api/admin/dashboard");
        check("概览：模板数量、今天的调用、近 14 天每天的调用量、调用最多的模板",
            dashboard.GetProperty("templates").GetProperty("total").GetInt32() >= 2 && dashboard.GetProperty("days").GetArrayLength() == 14
            && dashboard.GetProperty("today").GetProperty("total").GetInt32() > 0 && dashboard.GetProperty("topTemplates").GetArrayLength() > 0);
        var slowest = dashboard.GetProperty("slowTemplates");
        check("概览带今日单张 P95 和近 7 天最慢的模板（设计书第 9 节：工作台显示今日 P95、慢调用数、最慢的模板）",
            dashboard.GetProperty("today").GetProperty("p95Ms").GetDouble() > 0 && dashboard.GetProperty("today").TryGetProperty("slow", out _)
            && slowest.GetArrayLength() > 0 && slowest[0].GetProperty("averageMs").GetDouble() >= slowest[slowest.GetArrayLength() - 1].GetProperty("averageMs").GetDouble());

        var test = await designer.PostAsync("/api/admin/api-test/render", new
        {
            templateCode = "BOX_LABEL",
            format = "png",
            data = JsonDocument.Parse(Fixtures.BoxLabelData).RootElement
        });
        var adminTest = await admin.PostAsync("/api/admin/api-test/render", new
        {
            templateCode = "BOX_LABEL",
            format = "png",
            data = JsonDocument.Parse(Fixtures.BoxLabelData).RootElement
        });
        check("接口调试：用后台登录试生成（不需要 Key），按业务范围限制模板，没有 api.test 权限 403",
            (int)test.StatusCode == 404 && adminTest.IsSuccessStatusCode && adminTest.Content.Headers.ContentType?.MediaType == "image/png"
            && await AdminSession.CodeAsync(await viewer.PostAsync("/api/admin/api-test/render", new
            {
                templateCode = "BOX_LABEL",
                format = "png"
            })) == "FORBIDDEN");

        var ops = await admin.JsonAsync(HttpMethod.Get, "/api/admin/op-logs?pageSize=200");
        var types = ops.EnumerateArray().Select(item => item.GetProperty("type").GetString()).ToHashSet();
        check("业务分组、模板、调用方、画布预设、素材的写操作都记了操作日志",
            types.IsSupersetOf(["业务分组", "新建模板", "编辑模板", "发布模板", "回滚版本", "删除模板", "调用方", "画布预设", "素材"]));
    }

    private static Dictionary<string, object> Template(string code, string name, string text) => new()
    {
        ["code"] = code,
        ["name"] = name,
        ["page"] = new
        {
            width = 80,
            height = 50,
            dpi = 203
        },
        ["fields"] = new object[] { new { key = "lot", name = "批次", type = "string", required = true, sample = "L001" } },
        ["elements"] = new object[] { new { id = "t1", type = "text", x = 2, y = 2, width = 60, height = 8, content = text } },
    };

    private static async Task<int> RenderStatus(HttpClient http, string apiKey, string template)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { templateCode = template, format = "png", data = new { lot = "A1", partName = "x", partNo = "x", qty = 1, batchNo = "x", prodDate = "2026-10-08", boxNo = "C1" } }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(HeaderNames.ApiKey, apiKey);
        return (int)(await http.SendAsync(request)).StatusCode;
    }

    private static async Task<string?> RenderCode(HttpClient http, string template)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/render")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { templateCode = template, format = "png", data = new { lot = "A1" } }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
        return await AdminSession.CodeAsync(await http.SendAsync(request));
    }

    private static byte[] Png()
    {
        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.Red);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}

/// <summary>
/// 管理后台检查的入口：用临时数据目录启动真实服务，先跑账号和权限，再跑各项管理功能。
/// </summary>
internal static class AdminChecks
{
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory);
            using var http = factory.CreateClient();
            var admin = await AdminAccountChecks.RunAsync(http, check);
            await AdminResourceChecks.RunAsync(http, admin, check);
            await PreviewChecks.RunAsync(backendRoot, http, admin, check);
            await LayoutServerChecks.RunAdminAsync(http, admin, check);
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }
    }
}
