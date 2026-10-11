using System.Text.Json;
using LabelService.Contracts.Protocol;
using LabelService.Infrastructure.Persistence;
using LabelService.Infrastructure.Persistence.PostgreSql;
using LabelService.Infrastructure.Persistence.Sqlite;
using LabelService.Infrastructure.Persistence.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LabelService.Backend.Checks;

/// <summary>
/// 多数据库（ADR 0010）：三种数据库的迁移都和模型一致、按配置选中对应的上下文；
/// 设置了环境变量时，再在真实的 SQL Server、PostgreSQL 上跑一遍主要流程（建库、登录、业务分组、模板发布、出图、调用日志、概览），跑完删库。
/// </summary>
/// <remarks>
/// 环境变量的值是连接字符串，不用写库名（检查程序每次新建一个临时库）：
/// <c>LABEL_TEST_SQLSERVER=Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true</c>、
/// <c>LABEL_TEST_POSTGRES=Host=localhost;Username=postgres;Password=…</c>。没设置时跳过，只打一行提示。
/// </remarks>
internal static class DatabaseProviderChecks
{
    /// <summary>
    /// SQL Server 连接字符串的环境变量。
    /// </summary>
    public const string SqlServerVariable = "LABEL_TEST_SQLSERVER";

    /// <summary>
    /// PostgreSQL 连接字符串的环境变量。
    /// </summary>
    public const string PostgreSqlVariable = "LABEL_TEST_POSTGRES";

    /// <summary>
    /// 运行全部检查。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>完成时完成。</returns>
    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        CheckMigrations(check);
        CheckProviderSelection(check);
        await RunOnServerAsync(backendRoot, DatabaseProvider.SqlServer, Environment.GetEnvironmentVariable(SqlServerVariable), check);
        await RunOnServerAsync(backendRoot, DatabaseProvider.PostgreSql, Environment.GetEnvironmentVariable(PostgreSqlVariable), check);
    }

    /// <summary>
    /// 每种数据库的主库、日志库都有迁移，迁移的快照和当前模型一致（改了实体要给三种数据库各生成一次迁移）。不需要连接数据库。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    private static void CheckMigrations(Action<string, bool> check)
    {
        const string sqlServer = "Server=(localdb)\\MSSQLLocalDB;Database=unused";
        const string postgreSql = "Host=localhost;Database=unused";
        DbContext[] contexts =
        [
            new SqliteLabelDbContext(new DbContextOptionsBuilder<SqliteLabelDbContext>().UseSqlite("Data Source=:memory:").Options),
            new SqliteLogDbContext(new DbContextOptionsBuilder<SqliteLogDbContext>().UseSqlite("Data Source=:memory:").Options),
            new SqlServerLabelDbContext(new DbContextOptionsBuilder<SqlServerLabelDbContext>().UseSqlServer(sqlServer).Options),
            new SqlServerLogDbContext(new DbContextOptionsBuilder<SqlServerLogDbContext>().UseSqlServer(sqlServer).Options),
            new PostgreSqlLabelDbContext(new DbContextOptionsBuilder<PostgreSqlLabelDbContext>().UseNpgsql(postgreSql).Options),
            new PostgreSqlLogDbContext(new DbContextOptionsBuilder<PostgreSqlLogDbContext>().UseNpgsql(postgreSql).Options),
        ];
        foreach (var db in contexts)
        {
            using (db)
            {
                var name = db.GetType().Name;
                check($"{name}：有迁移，迁移和模型一致", db.Database.GetMigrations().Any() && !db.Database.HasPendingModelChanges());
            }
        }
    }

    /// <summary>
    /// 按 LabelService:Data:Provider 选中对应的上下文；SQL Server、PostgreSQL 的日志库用单独的迁移记录表（可以和主库同库）。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    private static void CheckProviderSelection(Action<string, bool> check)
    {
        (DatabaseProvider Provider, Type Main, Type Log)[] expected =
        [
            (DatabaseProvider.Sqlite, typeof(SqliteLabelDbContext), typeof(SqliteLogDbContext)),
            (DatabaseProvider.SqlServer, typeof(SqlServerLabelDbContext), typeof(SqlServerLogDbContext)),
            (DatabaseProvider.PostgreSql, typeof(PostgreSqlLabelDbContext), typeof(PostgreSqlLogDbContext)),
        ];
        foreach (var (provider, mainType, logType) in expected)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LabelService:Data:Provider"] = provider.ToString(),
                ["LabelService:Data:ConnectionString"] = provider == DatabaseProvider.PostgreSql ? "Host=localhost;Database=unused" : "Server=(localdb)\\MSSQLLocalDB;Database=unused",
                ["LabelService:Data:Directory"] = Path.GetTempPath(),
            }).Build();
            using var services = new ServiceCollection().AddLogging().AddLabelPersistence(configuration).BuildServiceProvider();
            using var main = services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContext();
            using var log = services.GetRequiredService<IDbContextFactory<LogDbContext>>().CreateDbContext();
            var history = RelationalOptionsExtension.Extract(log.GetService<IDbContextOptions>()).MigrationsHistoryTableName;
            var separate = provider == DatabaseProvider.Sqlite ? history is null : history == PersistenceProviders.LogMigrationsHistoryTable;
            check($"Provider={provider} 时用 {mainType.Name}、{logType.Name}，日志库的迁移记录表{(provider == DatabaseProvider.Sqlite ? "用默认的" : "单独一张")}",
                main.GetType() == mainType && log.GetType() == logType && separate);
        }
    }

    /// <summary>
    /// 在真实数据库上跑主要流程。连接字符串为空时跳过。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="provider">数据库种类。</param>
    /// <param name="connectionString">不带库名的连接字符串。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>完成时完成。</returns>
    private static async Task RunOnServerAsync(string backendRoot, DatabaseProvider provider, string? connectionString, Action<string, bool> check)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var variable = provider == DatabaseProvider.SqlServer ? SqlServerVariable : PostgreSqlVariable;
            Console.WriteLine($"INFO: 没有设置 {variable}，跳过 {provider} 上的端到端检查");
            return;
        }

        // 每次一个新库，跑完删掉。
        var database = "label_checks_" + Guid.NewGuid().ToString("N")[..12];
        var withDatabase = provider == DatabaseProvider.SqlServer
            ? new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database }.ToString()
            : new NpgsqlConnectionStringBuilder(connectionString) { Database = database }.ToString();
        var dataDirectory = TempDataDirectory.Create();
        try
        {
            await using var factory = new ServerFactory(backendRoot, dataDirectory, new Dictionary<string, string?>
            {
                ["LabelService:Data:Provider"] = provider.ToString(),
                ["LabelService:Data:ConnectionString"] = withDatabase,
            });
            using var http = factory.CreateClient();
            try
            {
                await CheckFlowAsync(backendRoot, factory.Services, http, provider, check);
            }
            finally
            {
                await using var db = await factory.Services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContextAsync();
                await db.Database.EnsureDeletedAsync();
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            NpgsqlConnection.ClearAllPools();
            TempDataDirectory.Delete(dataDirectory);
        }
    }

    /// <summary>
    /// 主要流程：迁移、示例模板出图、登录改密码、业务分组、新建并发布模板、调用日志、概览、画布预设、删除模板。
    /// </summary>
    /// <param name="backendRoot">backend 目录（读示例模板）。</param>
    /// <param name="services">服务的根容器。</param>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="provider">数据库种类（检查描述用）。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>完成时完成。</returns>
    private static async Task CheckFlowAsync(string backendRoot, IServiceProvider services, HttpClient http, DatabaseProvider provider, Action<string, bool> check)
    {
        await using (var main = await services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContextAsync())
        await using (var log = await services.GetRequiredService<IDbContextFactory<LogDbContext>>().CreateDbContextAsync())
        {
            check($"{provider}：启动时建库并执行了主库、日志库的全部迁移",
                !(await main.Database.GetPendingMigrationsAsync()).Any() && !(await log.Database.GetPendingMigrationsAsync()).Any()
                && (await main.Database.GetAppliedMigrationsAsync()).Any() && (await log.Database.GetAppliedMigrationsAsync()).Any());
        }

        var render = await ServerChecks.Post(http, $$"""{"templateCode":"BOX_LABEL","format":"png","data":{{Fixtures.BoxLabelData}}}""");
        check($"{provider}：开发环境导入了示例模板，开放接口出图", render.IsSuccessStatusCode && ServerChecks.Header(render, HeaderNames.TemplateVersion) == "1");

        var (_, admin, _) = await AdminSession.LoginAsync(http, "admin", "admin123");
        var changed = admin is not null && (await admin.PostAsync("/api/admin/me/password", new
        {
            oldPassword = "admin123",
            newPassword = AdminAccountChecks.AdminPassword
        })).IsSuccessStatusCode;
        check($"{provider}：admin 登录、改密码", changed);
        if (admin is null || !changed)
        {
            return;
        }

        var business = await admin.JsonAsync(HttpMethod.Post, "/api/admin/businesses", new
        {
            code = "PROVIDER",
            name = "数据库检查"
        });
        var duplicate = await AdminSession.CodeAsync(await admin.PostAsync("/api/admin/businesses", new
        {
            code = "provider",
            name = "重复"
        }));
        check($"{provider}：新建业务分组；编码重复（大小写不同）返回 400", business.GetProperty("code").GetString() == "PROVIDER" && duplicate == "INVALID");

        var template = Fixtures.LoadTemplate(backendRoot);
        template.Code = "PROVIDER_LABEL";
        var created = await admin.PostAsync("/api/admin/templates", new
        {
            code = template.Code,
            name = "数据库检查",
            businessId = business.GetProperty("id").GetInt32(),
            content = template
        });
        var published = await admin.JsonAsync(HttpMethod.Post, $"/api/admin/templates/{template.Code}/publish", new
        {
            note = "检查"
        });
        var output = await ServerChecks.Post(http, $$"""{"templateCode":"PROVIDER_LABEL","format":"pdf","data":{{Fixtures.BoxLabelData}}}""");
        check($"{provider}：新建模板、发布版本 1、开放接口立即可用",
            created.IsSuccessStatusCode && published.GetProperty("version").GetInt32() == 1 && output.IsSuccessStatusCode);

        var logs = JsonDocument.Parse("[]").RootElement;
        for (var i = 0; i < 100 && logs.GetArrayLength() == 0; i++)
        {
            await Task.Delay(50);
            logs = await admin.JsonAsync(HttpMethod.Get, "/api/admin/call-logs?templateCode=PROVIDER_LABEL");
        }

        check($"{provider}：调用日志写入日志库并能按模板查询", logs.GetArrayLength() == 1 && logs[0].GetProperty("status").GetString() == "ok");
        var dashboard = await admin.JsonAsync(HttpMethod.Get, "/api/admin/dashboard");
        check($"{provider}：概览的统计（今日调用、近 14 天、最多和最慢的模板）都能查",
            dashboard.GetProperty("today").GetProperty("total").GetInt32() >= 2 && dashboard.GetProperty("days").GetArrayLength() == 14
            && dashboard.GetProperty("topTemplates").GetArrayLength() >= 1 && dashboard.GetProperty("recentOps").GetArrayLength() >= 1);

        var preset = await admin.JsonAsync(HttpMethod.Post, "/api/admin/presets", new
        {
            name = "检查用",
            width = 70,
            height = 40
        });
        check($"{provider}：新建画布预设不和种子数据的 ID 冲突", preset.GetProperty("id").GetInt32() > 8);

        var deleted = await admin.DeleteAsync($"/api/admin/templates/{template.Code}");
        var gone = await ServerChecks.Post(http, $$"""{"templateCode":"PROVIDER_LABEL","format":"png","data":{{Fixtures.BoxLabelData}}}""");
        check($"{provider}：删除模板后开放接口返回 404", deleted.IsSuccessStatusCode && (int)gone.StatusCode == 404);
    }
}
