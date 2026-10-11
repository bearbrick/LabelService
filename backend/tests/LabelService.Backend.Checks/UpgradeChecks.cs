using System.Data.Common;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using LabelService.Contracts.Protocol;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using LabelService.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelService.Backend.Checks;

/// <summary>
/// 旧库升级（T-021）：T-008、T-009 时期的库（迁移到 AddApiClients）升级到业务分组授权时，模板分类、调用方授权按 <see cref="LegacyUpgrade"/> 换算，
/// 授权不扩大也不缩小；升级前自动备份，迁移中途失败（故障注入）能恢复到升级前，重试成功。
/// </summary>
internal static class UpgradeChecks
{
    private static readonly (string Name, string Key, string Allowed)[] Clients =
    [
        ("MES", "lbl_test_legacy_mes", "*"),
        ("WMS", "lbl_test_legacy_wms", "BOX_LABEL"),
        ("ERP", "lbl_test_legacy_erp", "BOX2, A4_SHIPPING_MARK, GONE"),
    ];

    public static async Task RunAsync(string backendRoot, Action<string, bool> check)
    {
        CheckPlan(check);
        await CheckFaultInjectionAsync(backendRoot, check);
        await CheckServerAsync(backendRoot, check);
    }

    /// <summary>
    /// 换算规则：逐个调用方、逐个模板核对升级前后能不能用，结果必须一样。
    /// </summary>
    private static void CheckPlan(Action<string, bool> check)
    {
        var snapshot = Snapshot();
        var plan = LegacyUpgrade.Plan(snapshot);
        var businessOf = plan.Businesses.SelectMany(business => business.TemplateIds.Select(id => (id, business.Code))).ToDictionary(item => item.id, item => item.Code);
        var exact = snapshot.Clients.All(client =>
        {
            var grant = plan.Grants.Single(item => item.ClientId == client.Id);
            var allowed = client.AllowedTemplates.Split(',', StringSplitOptions.TrimEntries);
            return snapshot.Templates.All(template =>
                (allowed.Contains("*") || allowed.Contains(template.Code)) == (grant.AllBusinesses || grant.BusinessCodes.Contains(businessOf[template.Id])));
        });
        check("旧库换算：每个调用方升级后能用的模板和升级前完全一样（* 换成全部业务，其余按模板所在分组授权）", exact);
        check("同一分类里能用的调用方不同的模板拆成“分类（2）”，没有分类的进“未分组”，库里不存在的模板编码记下来",
            plan.Businesses.Select(business => business.Name).SequenceEqual(["包装", "包装（2）", LegacyUpgrade.Ungrouped])
            && plan.Grants.Single(grant => grant.Name == "ERP").UnknownTemplates.SequenceEqual(["GONE"]));
    }

    /// <summary>
    /// 故障注入：非事务的 PRAGMA foreign_keys = 0 执行成功之后抛异常（和验收的复现一致），库要恢复到升级前，重试成功。
    /// </summary>
    private static async Task CheckFaultInjectionAsync(string backendRoot, Action<string, bool> check)
    {
        var directory = TempDataDirectory.Create();
        try
        {
            await CreateLegacyAsync(backendRoot, directory);
            var fault = new FaultAfterPragma();
            Exception? failure = null;
            try
            {
                await DatabaseUpgrade.UpgradeMainAsync(() => Task.FromResult(Context(directory, fault)), directory, TimeProvider.System, NullLogger.Instance);
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            await using (var connection = Open(directory))
            {
                var applied = await Strings(connection, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId");
                var columns = await Strings(connection, "SELECT name FROM pragma_table_info('label_template')");
                var categories = await Strings(connection, "SELECT category FROM label_template WHERE category IS NOT NULL ORDER BY id");
                var allowed = await Strings(connection, "SELECT allowed_templates FROM api_client ORDER BY id");
                check("迁移中途失败（PRAGMA foreign_keys = 0 之后注入异常）：升级报错、服务不启动",
                    fault.Thrown && failure is not null && failure.Message.Contains("已恢复到升级前的备份", StringComparison.Ordinal));
                check("失败后库恢复到升级前：迁移记录仍到 AddApiClients，分类列和调用方授权原样都在",
                    applied.SequenceEqual(["20261009030555_InitialCreate", LegacyUpgrade.ApiClientsMigration])
                    && columns.Contains("category") && categories.SequenceEqual(["包装", "包装"])
                    && allowed.SequenceEqual(Clients.Select(client => client.Allowed)));
            }

            check("升级前的备份留在数据目录的 backups/", Directory.EnumerateFiles(Path.Combine(directory, "backups"), "label-before-upgrade-*.db").Any());

            await DatabaseUpgrade.UpgradeMainAsync(() => Task.FromResult(Context(directory, null)), directory, TimeProvider.System, NullLogger.Instance);
            await using (var connection = Open(directory))
            {
                var businesses = await Strings(connection, "SELECT name FROM business ORDER BY id");
                var templates = await Strings(connection, "SELECT t.code || '=' || b.name FROM label_template t JOIN business b ON b.id = t.business_id ORDER BY t.id");
                var clients = await Strings(connection, "SELECT name || ':' || all_businesses || ':' || business_ids FROM api_client ORDER BY id");
                check("恢复后重试升级成功（不再出现 duplicate column），分类和授权换算进了业务分组",
                    businesses.SequenceEqual(["包装", "包装（2）", LegacyUpgrade.Ungrouped])
                    && templates.SequenceEqual(["BOX_LABEL=包装", "BOX2=包装（2）", "A4_SHIPPING_MARK=" + LegacyUpgrade.Ungrouped])
                    && clients[0] == "MES:1:" && clients[1] == "WMS:0:1" && clients[2] == "ERP:0:2,3");
            }
        }
        finally
        {
            TempDataDirectory.Delete(directory);
        }
    }

    /// <summary>
    /// 端到端：用旧库启动服务，旧的 API Key 按原来的授权访问模板。
    /// </summary>
    private static async Task CheckServerAsync(string backendRoot, Action<string, bool> check)
    {
        var directory = TempDataDirectory.Create();
        try
        {
            await CreateLegacyAsync(backendRoot, directory);
            await using var factory = new ServerFactory(backendRoot, directory);
            using var http = factory.CreateClient();
            async Task<HttpStatusCode> Schema(string key, string code)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/templates/{code}/schema");
                request.Headers.Add(HeaderNames.ApiKey, key);
                return (await http.SendAsync(request)).StatusCode;
            }

            var results = new List<string>();
            foreach (var (name, key, allowed) in Clients)
            {
                foreach (var code in new[] { "BOX_LABEL", "BOX2", "A4_SHIPPING_MARK" })
                {
                    var expected = allowed == "*" || allowed.Split(',', StringSplitOptions.TrimEntries).Contains(code) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
                    var actual = await Schema(key, code);
                    if (actual != expected)
                    {
                        results.Add($"{name} {code} 应 {(int)expected} 实际 {(int)actual}");
                    }
                }
            }

            check("用旧库启动服务：旧 API Key 能用的模板和升级前一样，不能用的返回 403" + (results.Count > 0 ? "：" + string.Join("；", results) : ""), results.Count == 0);
        }
        finally
        {
            TempDataDirectory.Delete(directory);
        }
    }

    private static LegacySnapshot Snapshot() => new(
        [new LegacyTemplate(1, "BOX_LABEL", "包装"), new LegacyTemplate(2, "BOX2", "包装"), new LegacyTemplate(3, "A4_SHIPPING_MARK", null)],
        Clients.Select((client, index) => new LegacyClient(index + 1, client.Name, client.Allowed)).ToList());

    /// <summary>
    /// 造一个 AddApiClients 版本的旧库：三个已发布的模板（两个分类“包装”、一个没有分类）、三个调用方。
    /// </summary>
    private static async Task CreateLegacyAsync(string backendRoot, string directory)
    {
        await using (var db = Context(directory, null))
        {
            await db.GetService<IMigrator>().MigrateAsync(LegacyUpgrade.ApiClientsMigration);
        }

        var box = await File.ReadAllTextAsync(Path.Combine(backendRoot, "samples", "templates", "box-label.json"));
        var a4 = await File.ReadAllTextAsync(Path.Combine(backendRoot, "samples", "templates", "a4-shipping-mark.json"));
        await using var connection = Open(directory);
        var now = "2026-10-09 06:00:00";
        foreach (var (id, code, category, content) in new[] { (1, "BOX_LABEL", "'包装'", box), (2, "BOX2", "'包装'", box.Replace("\"BOX_LABEL\"", "\"BOX2\"", StringComparison.Ordinal)), (3, "A4_SHIPPING_MARK", "NULL", a4) })
        {
            await Execute(connection, $"INSERT INTO label_template (id, code, name, category, status, latest_version, created_by, updated_at) VALUES ({id}, '{code}', '{code}', {category}, 'published', 1, 'system', '{now}')");
            await Execute(connection, $"INSERT INTO label_template_version (template_id, version_no, content_json, status, published_by, published_at) VALUES ({id}, 1, $content, 'published', 'system', '{now}')", content);
        }

        foreach (var (name, key, allowed) in Clients)
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            await Execute(connection, $"INSERT INTO api_client (name, api_key_hash, key_prefix, allowed_templates, enabled, created_at, updated_at) VALUES ('{name}', '{hash}', '{key[..12]}', '{allowed}', 1, '{now}', '{now}')");
        }
    }

    private static LabelDbContext Context(string directory, IInterceptor? interceptor)
    {
        var builder = new DbContextOptionsBuilder<SqliteLabelDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, DataOptions.DatabaseFileName) }.ToString());
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new SqliteLabelDbContext(builder.Options);
    }

    private static SqliteConnection Open(string directory)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, DataOptions.DatabaseFileName), Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static async Task Execute(SqliteConnection connection, string sql, string? content = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (content is not null)
        {
            command.Parameters.AddWithValue("$content", content);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> Strings(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return values;
    }

    /// <summary>
    /// PRAGMA foreign_keys = 0 执行成功后，下一条命令抛异常（模拟迁移中途进程出错）。
    /// </summary>
    private sealed class FaultAfterPragma : DbCommandInterceptor
    {
        private bool armed;

        public bool Thrown
        {
            get; private set;
        }

        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            Arm(command);
            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Arm(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Fail();
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Fail();
            return ValueTask.FromResult(result);
        }

        private void Arm(DbCommand command)
        {
            if (command.CommandText.Contains("PRAGMA foreign_keys = 0", StringComparison.OrdinalIgnoreCase))
            {
                armed = true;
            }
        }

        private void Fail()
        {
            if (armed && !Thrown)
            {
                Thrown = true;
                throw new InvalidOperationException("注入的故障：迁移中途出错");
            }
        }
    }
}
