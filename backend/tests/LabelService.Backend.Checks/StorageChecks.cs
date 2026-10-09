using System.Diagnostics;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Server.Data;
using LabelService.Server.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LabelService.Backend.Checks;

/// <summary>
/// 模板存储（T-008）：SQLite 建库和迁移、草稿和发布、发布后不可修改、开放接口不访问数据库、重启后从库加载。
/// 用临时数据目录，跑完删除。
/// </summary>
internal static class StorageChecks
{
    private const string BoxLabel = "BOX_LABEL";

    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using (var factory = new ServerFactory(backendRoot, dataDirectory))
            {
                using var http = factory.CreateClient();
                await CheckDatabaseAsync(factory.Services, dataDirectory, check);
                await CheckDraftAndPublishAsync(factory.Services, http, backendRoot, check);
                await CheckPublishRulesAsync(factory.Services, http, backendRoot, check);
                await CheckImmutableAsync(factory.Services, backendRoot, check);
                await CheckHotPathAsync(factory.Services, http, check);
            }

            await using (var restarted = new ServerFactory(backendRoot, dataDirectory))
            {
                using var http = restarted.CreateClient();
                var templates = await restarted.Services.GetRequiredService<TemplateRepository>().ListAsync();
                check("重启后从数据库加载已发布版本：最新版本仍是 2，示例模板不重复导入",
                    await Version(http, BoxLabel) == "2" && await Version(http, BoxLabel, 1) == "1" && templates.Count(item => item.Code == BoxLabel) == 1);
            }

            await using (var files = new ServerFactory(backendRoot, dataDirectory, new Dictionary<string, string?> { ["LabelService:Templates:Source"] = "Files" }))
            {
                using var http = files.CreateClient();
                check("模板来源配置为 Files 时从示例目录加载，版本固定为 1",
                    files.Services.GetRequiredService<ITemplateStore>() is FileTemplateStore && await Version(http, BoxLabel) == "1");
            }
        }
        finally
        {
            TempDataDirectory.Delete(dataDirectory);
        }

        check("检查程序的临时数据目录已删除", !Directory.Exists(dataDirectory));
    }

    private static async Task CheckDatabaseAsync(IServiceProvider services, string dataDirectory, Action<string, bool> check)
    {
        check("启动时在数据目录自动建库 label.db", File.Exists(Path.Combine(dataDirectory, DataOptions.DatabaseFileName)));
        await using var db = await services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContextAsync();
        check("启动时执行了全部迁移", (await db.Database.GetAppliedMigrationsAsync()).Any() && !(await db.Database.GetPendingMigrationsAsync()).Any());
        check("实体和迁移一致（改了 Server/Data 的实体要生成迁移，命令见 backend/README.md）", !db.Database.HasPendingModelChanges());

        await db.Database.OpenConnectionAsync();
        async Task<string?> Pragma(string name)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA " + name;
            return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }

        check("连接打开时设置了 WAL、synchronous=NORMAL、busy_timeout=5000、外键检查",
            await Pragma("journal_mode") == "wal" && await Pragma("synchronous") == "1"
            && await Pragma("busy_timeout") == "5000" && await Pragma("foreign_keys") == "1");
        var presets = await db.CanvasPresets.OrderBy(preset => preset.Sort).ToListAsync();
        check("建库时写入默认画布预设（和设计器一致）", presets.Count == 8 && presets[0] is { Name: "A4", WidthMm: 210, HeightMm: 297 });
    }

    private static async Task CheckDraftAndPublishAsync(IServiceProvider services, HttpClient http, string backendRoot, Action<string, bool> check)
    {
        var repository = services.GetRequiredService<TemplateRepository>();
        var store = services.GetRequiredService<DbTemplateStore>();
        var box = (await repository.ListAsync()).SingleOrDefault(item => item.Code == BoxLabel);
        check("开发环境首次建库把示例模板导入为已发布版本 1",
            box is { Status: TemplateStatuses.Published, LatestVersion: 1, HasDraft: false, Category: "示例" });
        check("时间列读回来是 UTC", box is { UpdatedAt.Kind: DateTimeKind.Utc } && (DateTime.UtcNow - box.UpdatedAt).Duration() < TimeSpan.FromMinutes(10));

        var template = Fixtures.LoadTemplate(backendRoot);
        template.Name = "成品箱标（第二版）";
        var first = await repository.SaveDraftAsync(template, "checks");
        var second = await repository.SaveDraftAsync(template, "checks");
        var drafts = await CountDraftsAsync(services, BoxLabel);
        check("保存草稿：版本号是最新发布版本 + 1，重复保存覆盖同一个草稿", first == 2 && second == 2 && drafts == 1);
        check("草稿不影响开放接口：仍然用版本 1", await Version(http, BoxLabel) == "1");

        var published = await repository.PublishAsync(BoxLabel, "checks");
        check("发布生成版本 2 并返回试渲染耗时", published is { Version: 2, RenderMs: > 0 });
        check("发布后立即生效：不指定版本用版本 2，指定版本 1 仍可用",
            await Version(http, BoxLabel) == "2" && await Version(http, BoxLabel, 1) == "1"
            && store.TryGetPublished(BoxLabel, null, out var latest) && latest.Template.Name == template.Name);
        check("发布后草稿转为版本，模板没有草稿", await repository.GetDraftAsync(BoxLabel) is null && await CountDraftsAsync(services, BoxLabel) == 0);

        var restored = await repository.RestoreToDraftAsync(BoxLabel, 1, "checks");
        check("回滚：把旧版本内容复制成草稿，版本号接着往后排",
            restored == 3 && (await repository.GetDraftAsync(BoxLabel))?.Name == Fixtures.LoadTemplate(backendRoot).Name);
        check("放弃草稿", await repository.DiscardDraftAsync(BoxLabel) && await repository.GetDraftAsync(BoxLabel) is null);
        check("没有草稿时不能发布", await CatchAsync(() => repository.PublishAsync(BoxLabel, "checks")) is not null);
    }

    private static async Task CheckPublishRulesAsync(IServiceProvider services, HttpClient http, string backendRoot, Action<string, bool> check)
    {
        var repository = services.GetRequiredService<TemplateRepository>();
        var store = services.GetRequiredService<DbTemplateStore>();

        var broken = Fixtures.LoadTemplate(backendRoot);
        broken.Elements.OfType<TextElement>().First().Content = "{{nope}}";
        broken.Elements.OfType<BarcodeElement>().First().Rotation = 45;
        await repository.SaveDraftAsync(broken, "checks");
        var error = await CatchAsync(() => repository.PublishAsync(BoxLabel, "checks"));
        check("发布前检查：引用未定义字段、一维码不是直角都会阻止发布，并列出全部问题",
            error is { Details.Count: 2 } && error.Details.Any(item => item.Contains("nope")) && error.Details.Any(item => item.Contains("45")));
        check("检查不通过时最新版本不变", store.TryGetPublished(BoxLabel, null, out var latest) && latest.Version == 2);
        await repository.DiscardDraftAsync(BoxLabel);

        var badCode = Fixtures.LoadTemplate(backendRoot);
        badCode.Code = "1 BAD";
        check("模板编码不合法时不能保存", await CatchAsync(() => repository.SaveDraftAsync(badCode, "checks")) is not null);

        var fresh = new LabelTemplate
        {
            Code = "NEW_LABEL",
            Name = "新标签",
            Page = new LabelPage { Width = 60, Height = 40 },
            Fields = [new FieldDefinition { Key = "lot", Type = FieldType.String }],
            Elements = [new TextElement { Id = "t1", Content = "批次 {{lot}}", X = 2, Y = 2, Width = 40, Height = 8 }],
        };
        await repository.SaveDraftAsync(fresh, "checks", "测试");
        var notFound = await ServerChecks.Post(http, """{"templateCode":"NEW_LABEL","format":"png","data":{"lot":"A1"}}""");
        check("新模板只有草稿时开放接口返回 404", (int)notFound.StatusCode == 404);
        var result = await repository.PublishAsync("NEW_LABEL", "checks");
        check("必填字段没有样例值时用占位值试渲染，并提示补样例",
            result.Version == 1 && result.Warnings.Any(item => item.Contains("lot") && item.Contains("样例")));
        var ok = await ServerChecks.Post(http, """{"templateCode":"NEW_LABEL","format":"png","data":{"lot":"A1"}}""");
        check("新模板发布后立即能调用", ok.IsSuccessStatusCode && ServerChecks.Header(ok, HeaderNames.TemplateVersion) == "1");
    }

    private static async Task CheckImmutableAsync(IServiceProvider services, string backendRoot, Action<string, bool> check)
    {
        var contexts = services.GetRequiredService<IDbContextFactory<LabelDbContext>>();
        await using (var db = await contexts.CreateDbContextAsync())
        {
            var version = await db.TemplateVersions.SingleAsync(item => item.Template!.Code == BoxLabel && item.VersionNo == 1);
            version.ContentJson = "{}";
            check("已发布版本的内容不能修改", await ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()));
        }

        await using (var db = await contexts.CreateDbContextAsync())
        {
            db.TemplateVersions.Remove(await db.TemplateVersions.SingleAsync(item => item.Template!.Code == BoxLabel && item.VersionNo == 1));
            check("已发布版本不能删除", await ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()));
        }

        // 两个人同时操作同一个草稿：A 发布了，B 还拿着旧的草稿保存。状态是并发令牌，B 必须失败，不能改到已发布的版本。
        var race = Fixtures.LoadTemplate(backendRoot);
        race.Code = "RACE_LABEL";
        await services.GetRequiredService<TemplateRepository>().SaveDraftAsync(race, "checks");
        await using var a = await contexts.CreateDbContextAsync();
        await using var b = await contexts.CreateDbContextAsync();
        var draftA = await a.TemplateVersions.SingleAsync(item => item.Template!.Code == "RACE_LABEL");
        var draftB = await b.TemplateVersions.SingleAsync(item => item.Template!.Code == "RACE_LABEL");
        draftA.Status = TemplateStatuses.Published;
        await a.SaveChangesAsync();
        draftB.ContentJson = "{}";
        check("并发：草稿已被别人发布时，用旧数据保存会失败", await ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync()));
    }

    private static async Task CheckHotPathAsync(IServiceProvider services, HttpClient http, Action<string, bool> check)
    {
        using var counter = new CommandCounter();
        for (var i = 0; i < 5; i++)
        {
            await Version(http, BoxLabel);
            await Version(http, BoxLabel, 1);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/templates/{BoxLabel}/schema");
            request.Headers.Add(HeaderNames.ApiKey, Fixtures.DevApiKey);
            (await http.SendAsync(request)).EnsureSuccessStatusCode();
        }

        var duringRequests = counter.Count;
        await services.GetRequiredService<TemplateRepository>().ListAsync();
        check($"开放接口取模板不访问数据库（生成、字段定义各调用多次，数据库命令 {duringRequests} 条）", duringRequests == 0 && counter.Count > 0);
    }

    private static async Task<string?> Version(HttpClient http, string code, int? version = null)
    {
        var versionJson = version is null ? "" : $""","version":{version}""";
        var response = await ServerChecks.Post(http, $$"""{"templateCode":"{{code}}","format":"png"{{versionJson}},"data":{{Fixtures.BoxLabelData}}}""");
        return response.IsSuccessStatusCode ? ServerChecks.Header(response, HeaderNames.TemplateVersion) : $"HTTP {(int)response.StatusCode}";
    }

    private static async Task<int> CountDraftsAsync(IServiceProvider services, string code)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContextAsync();
        return await db.TemplateVersions.CountAsync(item => item.Template!.Code == code && item.Status == TemplateStatuses.Draft);
    }

    private static async Task<TemplateStoreException?> CatchAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (TemplateStoreException ex)
        {
            return ex;
        }
    }

    private static async Task<bool> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    /// <summary>
    /// 数一段时间内 EF Core 执行的数据库命令。订阅 EF Core 的诊断事件，不需要改产品代码。
    /// </summary>
    private sealed class CommandCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> subscriptions = [];
        private int count;

        public CommandCounter() => subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));

        public int Count => Volatile.Read(ref count);

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == DbLoggerCategory.Name)
            {
                lock (subscriptions)
                {
                    subscriptions.Add(value.Subscribe(this));
                }
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == RelationalEventId.CommandExecuting.Name)
            {
                Interlocked.Increment(ref count);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            lock (subscriptions)
            {
                subscriptions.ForEach(subscription => subscription.Dispose());
            }
        }
    }
}
