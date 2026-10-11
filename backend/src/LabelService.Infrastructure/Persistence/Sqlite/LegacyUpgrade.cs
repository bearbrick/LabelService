using System.Data.Common;
using LabelService.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LabelService.Infrastructure.Persistence.Sqlite;

/// <summary>
/// 旧库升级的数据转换：T-008、T-009 时期的库（迁移到 AddApiClients 为止）用“模板分类 category”和“调用方允许的模板编码 allowed_templates”，
/// T-014 起（迁移 AddAdmin，ADR 0006）改成业务分组授权，这两列被删掉。升级时先读出旧数据，迁移完再换算成业务分组，授权不扩大也不缩小。
/// </summary>
/// <remarks>
/// <para>
/// 换算规则：模板按“分类 + 能用它的调用方集合”分组，每组一个业务分组（名称用分类，没有分类的叫“未分组”；
/// 同一分类里能用的调用方不同的模板拆成“分类（2）”等）。调用方原来能用哪些模板，就授权那些模板所在的分组；
/// 原来是 <c>*</c> 的授权全部业务。这样升级后每个调用方能用的模板和升级前完全一样。
/// </para>
/// <para>
/// 原来 allowed_templates 里写了、但库里不存在的模板编码无法换算，记在升级日志里（不影响已有模板）。
/// </para>
/// </remarks>
internal static class LegacyUpgrade
{
    /// <summary>升级前最后一个旧版迁移（有 api_client.allowed_templates）。</summary>
    public const string ApiClientsMigration = "20261009065040_AddApiClients";

    /// <summary>删掉 category、allowed_templates 的迁移。</summary>
    public const string AdminMigration = "20261009071504_AddAdmin";

    /// <summary>没有分类的模板放进的分组名称。</summary>
    public const string Ungrouped = "未分组";

    /// <summary>
    /// 升级前读出旧数据。只在 AddAdmin 还没执行时调用。
    /// </summary>
    /// <param name="connection">已打开的连接。</param>
    /// <param name="hasClients">库里有没有 api_client 表（AddApiClients 已执行）。</param>
    public static async Task<LegacySnapshot> CaptureAsync(DbConnection connection, bool hasClients)
    {
        var templates = new List<LegacyTemplate>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, code, category FROM label_template";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                templates.Add(new LegacyTemplate(reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }

        var clients = new List<LegacyClient>();
        if (hasClients)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, name, allowed_templates FROM api_client";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                clients.Add(new LegacyClient(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        return new LegacySnapshot(templates, clients);
    }

    /// <summary>
    /// 按换算规则算出要建的业务分组和每个调用方的授权。纯函数，不访问数据库。
    /// </summary>
    public static LegacyPlan Plan(LegacySnapshot snapshot)
    {
        var allowed = snapshot.Clients.ToDictionary(
            client => client.Id,
            client => client.AllowedTemplates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal));
        var wildcard = snapshot.Clients.Where(client => allowed[client.Id].Contains("*")).Select(client => client.Id).ToHashSet();
        string Category(LegacyTemplate template) => string.IsNullOrWhiteSpace(template.Category) ? Ungrouped : template.Category.Trim();
        int[] Signature(LegacyTemplate template) =>
            snapshot.Clients.Where(client => !wildcard.Contains(client.Id) && allowed[client.Id].Contains(template.Code)).Select(client => client.Id).Order().ToArray();

        var businesses = new List<LegacyBusiness>();
        foreach (var category in snapshot.Templates.GroupBy(Category).OrderBy(group => group.Min(template => template.Id)))
        {
            var groups = category.GroupBy(template => string.Join(",", Signature(template))).OrderBy(group => group.Min(template => template.Id)).ToList();
            for (var i = 0; i < groups.Count; i++)
            {
                var name = i == 0 ? category.Key : $"{category.Key}（{i + 1}）";
                var description = groups.Count == 1
                    ? $"旧版分类“{category.Key}”升级生成"
                    : $"旧版分类“{category.Key}”升级生成：分类里能用的调用方不同，按授权拆成 {groups.Count} 个分组";
                businesses.Add(new LegacyBusiness(
                    $"LEGACY_{businesses.Count + 1}",
                    name.Length > 64 ? name[..64] : name,
                    description,
                    groups[i].Select(template => template.Id).Order().ToList(),
                    groups[i].Key.Length == 0 ? [] : groups[i].Key.Split(',').Select(int.Parse).ToList()));
            }
        }

        var codes = snapshot.Templates.Select(template => template.Code).ToHashSet(StringComparer.Ordinal);
        var grants = snapshot.Clients.Select(client => new LegacyGrant(
            client.Id,
            client.Name,
            wildcard.Contains(client.Id),
            wildcard.Contains(client.Id) ? [] : businesses.Where(business => business.ClientIds.Contains(client.Id)).Select(business => business.Code).ToList(),
            allowed[client.Id].Where(code => code != "*" && !codes.Contains(code)).Order(StringComparer.Ordinal).ToList())).ToList();
        return new LegacyPlan(businesses, grants);
    }

    /// <summary>
    /// 迁移完成后写入：建业务分组、设置模板的分组、设置调用方的授权，在一个事务里。
    /// </summary>
    public static async Task ApplyAsync(LabelDbContext db, LegacyPlan plan, DateTime now, ILogger logger)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var business in plan.Businesses)
        {
            var record = new BusinessEntity { Code = business.Code, Name = business.Name, Description = business.Description, CreatedAt = now };
            db.Businesses.Add(record);
            await db.SaveChangesAsync();
            ids[business.Code] = record.Id;
            await db.Templates.Where(template => business.TemplateIds.Contains(template.Id))
                .ExecuteUpdateAsync(update => update.SetProperty(template => template.BusinessId, record.Id));
        }

        foreach (var grant in plan.Grants)
        {
            var businessIds = string.Join(",", grant.BusinessCodes.Select(code => ids[code]));
            await db.ApiClients.Where(client => client.Id == grant.ClientId)
                .ExecuteUpdateAsync(update => update.SetProperty(client => client.AllBusinesses, grant.AllBusinesses).SetProperty(client => client.BusinessIds, businessIds));
        }

        await transaction.CommitAsync();
        logger.LogWarning(
            "旧库升级：按模板分类和调用方授权建了 {Count} 个业务分组（{Businesses}）；调用方授权：{Grants}",
            plan.Businesses.Count,
            string.Join("；", plan.Businesses.Select(business => $"{business.Code} {business.Name} {business.TemplateIds.Count} 个模板")),
            string.Join("；", plan.Grants.Select(grant => grant.AllBusinesses ? $"{grant.Name} 全部业务" : $"{grant.Name} {string.Join("、", grant.BusinessCodes)}")));
        foreach (var grant in plan.Grants.Where(grant => grant.UnknownTemplates.Count > 0))
        {
            logger.LogWarning("旧库升级：调用方 {Client} 原来允许的模板 {Templates} 在库里不存在，无法换算", grant.Name, string.Join("、", grant.UnknownTemplates));
        }
    }
}

/// <summary>旧版模板。</summary>
internal sealed record LegacyTemplate(int Id, string Code, string? Category);

/// <summary>旧版调用方。</summary>
internal sealed record LegacyClient(int Id, string Name, string AllowedTemplates);

/// <summary>升级前读出的旧数据。</summary>
internal sealed record LegacySnapshot(IReadOnlyList<LegacyTemplate> Templates, IReadOnlyList<LegacyClient> Clients);

/// <summary>要建的业务分组：包含哪些模板、哪些调用方（不含 * 的）能用。</summary>
internal sealed record LegacyBusiness(string Code, string Name, string Description, IReadOnlyList<int> TemplateIds, IReadOnlyList<int> ClientIds);

/// <summary>一个调用方升级后的授权。</summary>
internal sealed record LegacyGrant(int ClientId, string Name, bool AllBusinesses, IReadOnlyList<string> BusinessCodes, IReadOnlyList<string> UnknownTemplates);

/// <summary>换算结果。</summary>
internal sealed record LegacyPlan(IReadOnlyList<LegacyBusiness> Businesses, IReadOnlyList<LegacyGrant> Grants);
