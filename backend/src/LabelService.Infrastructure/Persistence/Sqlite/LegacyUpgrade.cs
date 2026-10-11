using System.Data.Common;
using LabelService.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LabelService.Infrastructure.Persistence.Sqlite;

/// <summary>
/// 旧库升级的数据转换：一期早期的库（迁移到 AddApiClients 为止）用“模板分类 category”和“调用方允许的模板编码 allowed_templates”，
/// 有了管理后台以后（迁移 AddAdmin，ADR 0006）改成业务分组授权，这两列被删掉。升级时先读出旧数据，迁移完再换算成业务分组，授权不扩大也不缩小。
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
    /// <summary>
    /// 升级前最后一个旧版迁移（有 api_client.allowed_templates）。
    /// </summary>
    public const string ApiClientsMigration = "20261009065040_AddApiClients";

    /// <summary>
    /// 删掉 category、allowed_templates 的迁移。
    /// </summary>
    public const string AdminMigration = "20261009071504_AddAdmin";

    /// <summary>
    /// 没有分类的模板放进的分组名称。
    /// </summary>
    public const string Ungrouped = "未分组";

    /// <summary>
    /// 升级前读出旧数据。只在 AddAdmin 还没执行时调用。
    /// </summary>
    /// <param name="connection">已打开的连接。</param>
    /// <param name="hasClients">库里有没有 api_client 表（AddApiClients 已执行）。</param>
    /// <returns>旧的模板和调用方。</returns>
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
    /// <param name="snapshot">升级前读出的旧数据。</param>
    /// <returns>要建的业务分组和每个调用方的授权。</returns>
    public static LegacyPlan Plan(LegacySnapshot snapshot)
    {
        // 模板的“签名”是能用它的调用方集合（* 的调用方能用全部，不算在里面）；同一分类里签名相同的模板放进同一个分组。
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

        // 调用方授权：* 的授权全部业务，其余授权它能用的模板所在的分组；写了但库里没有的模板编码单独列出来记日志。
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
    /// <param name="db">迁移完成后的主库。</param>
    /// <param name="plan">换算结果。</param>
    /// <param name="now">业务分组的创建时间（UTC）。</param>
    /// <param name="logger">日志：记下建了哪些分组、每个调用方的授权和无法换算的模板编码。</param>
    /// <returns>写入完成时完成。</returns>
    public static async Task ApplyAsync(LabelDbContext db, LegacyPlan plan, DateTime now, ILogger logger)
    {
        // 在一个事务里完成：按旧的模板分类建业务分组并把模板归进去，再把调用方的模板授权换成分组授权。
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

        // 换算结果写进日志，管理员可以核对；原来授权的模板在库里不存在的单独警告。
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

/// <summary>
/// 旧版模板。
/// </summary>
/// <param name="Id">模板 ID。</param>
/// <param name="Code">模板编码。</param>
/// <param name="Category">旧版的模板分类；没有分类时为 null。</param>
internal sealed record LegacyTemplate(int Id, string Code, string? Category);

/// <summary>
/// 旧版调用方。
/// </summary>
/// <param name="Id">调用方 ID。</param>
/// <param name="Name">调用方名称。</param>
/// <param name="AllowedTemplates">允许的模板编码，逗号分隔；<c>*</c> 表示全部。</param>
internal sealed record LegacyClient(int Id, string Name, string AllowedTemplates);

/// <summary>
/// 升级前读出的旧数据。
/// </summary>
/// <param name="Templates">全部模板。</param>
/// <param name="Clients">全部调用方；库里还没有调用方表时为空。</param>
internal sealed record LegacySnapshot(IReadOnlyList<LegacyTemplate> Templates, IReadOnlyList<LegacyClient> Clients);

/// <summary>
/// 要建的业务分组：包含哪些模板、哪些调用方（不含 * 的）能用。
/// </summary>
/// <param name="Code">分组编码，LEGACY_1、LEGACY_2……</param>
/// <param name="Name">分组名称：旧分类名，按授权拆开时加“（2）”等。</param>
/// <param name="Description">说明：由哪个旧分类升级生成。</param>
/// <param name="TemplateIds">放进这个分组的模板 ID。</param>
/// <param name="ClientIds">能用这个分组的调用方 ID（不含 * 的调用方）。</param>
internal sealed record LegacyBusiness(string Code, string Name, string Description, IReadOnlyList<int> TemplateIds, IReadOnlyList<int> ClientIds);

/// <summary>
/// 一个调用方升级后的授权。
/// </summary>
/// <param name="ClientId">调用方 ID。</param>
/// <param name="Name">调用方名称（写日志用）。</param>
/// <param name="AllBusinesses">原来是 <c>*</c>，授权全部业务。</param>
/// <param name="BusinessCodes">授权的业务分组编码。</param>
/// <param name="UnknownTemplates">原来允许、但库里不存在的模板编码（无法换算，只记日志）。</param>
internal sealed record LegacyGrant(int ClientId, string Name, bool AllBusinesses, IReadOnlyList<string> BusinessCodes, IReadOnlyList<string> UnknownTemplates);

/// <summary>
/// 换算结果。
/// </summary>
/// <param name="Businesses">要建的业务分组。</param>
/// <param name="Grants">每个调用方的授权。</param>
internal sealed record LegacyPlan(IReadOnlyList<LegacyBusiness> Businesses, IReadOnlyList<LegacyGrant> Grants);
