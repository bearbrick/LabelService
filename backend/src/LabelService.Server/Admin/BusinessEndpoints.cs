using System.Text.RegularExpressions;
using LabelService.Server.Data;
using LabelService.Server.Templates;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Admin;

/// <summary>
/// 业务分组：列表按用户的业务范围过滤（登录即可看）；新建、修改、删除要 biz.manage。
/// </summary>
internal static partial class BusinessEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/businesses", ListAsync).RequireAdmin();
        api.MapPost("/businesses", CreateAsync).RequireAdmin(Permissions.BusinessManage);
        api.MapPut("/businesses/{id:int}", UpdateAsync).RequireAdmin(Permissions.BusinessManage);
        api.MapDelete("/businesses/{id:int}", DeleteAsync).RequireAdmin(Permissions.BusinessManage);
    }

    private static async Task<IResult> ListAsync(HttpContext http, IDbContextFactory<LabelDbContext> contexts)
    {
        var admin = http.CurrentAdmin();
        await using var db = await contexts.CreateDbContextAsync();
        var businesses = await db.Businesses.AsNoTracking().OrderBy(item => item.Sort).ThenBy(item => item.Id).ToListAsync();
        var counts = await db.Templates.Where(template => template.Status != TemplateStatuses.Deleted && template.BusinessId != null)
            .GroupBy(template => template.BusinessId!.Value)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count);
        return Results.Json(businesses.Where(item => admin.InScope(item.Id)).Select(item => View(item, counts.GetValueOrDefault(item.Id))));
    }

    private static async Task<IResult> CreateAsync(BusinessRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log, TimeProvider clock)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var business = new BusinessRecord { CreatedAt = clock.GetUtcNow().UtcDateTime, Sort = await db.Businesses.CountAsync() * 10 + 10 };
        if (await ApplyAsync(db, business, body) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "业务分组", $"{business.Code} {business.Name}", "新建业务分组");
        return Results.Json(View(business, 0));
    }

    private static async Task<IResult> UpdateAsync(int id, BusinessRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var business = await db.Businesses.FindAsync(id);
        if (business is null || !http.CurrentAdmin().InScope(id))
        {
            return AdminResults.NotFound("业务分组不存在");
        }

        if (await ApplyAsync(db, business, body) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        await db.SaveChangesAsync();
        await log.WriteAsync(http, "业务分组", $"{business.Code} {business.Name}", "修改业务分组");
        return Results.Json(View(business, await db.Templates.CountAsync(item => item.BusinessId == id && item.Status != TemplateStatuses.Deleted)));
    }

    private static async Task<IResult> DeleteAsync(int id, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var business = await db.Businesses.FindAsync(id);
        if (business is null || !http.CurrentAdmin().InScope(id))
        {
            return AdminResults.NotFound("业务分组不存在");
        }

        if (await db.Templates.AnyAsync(item => item.BusinessId == id && item.Status != TemplateStatuses.Deleted))
        {
            return AdminResults.Invalid("分组里还有模板，先把模板移到别的分组或删除");
        }

        // 已删除的模板只留档，解除关联；用户和调用方的业务范围里去掉这个分组。
        await db.Templates.Where(item => item.BusinessId == id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.BusinessId, (int?)null));
        foreach (var user in await db.Users.Where(item => !item.AllBusinesses && item.BusinessIds != "").ToListAsync())
        {
            user.BusinessIds = Without(user.BusinessIds, id);
        }

        foreach (var client in await db.ApiClients.Where(item => !item.AllBusinesses && item.BusinessIds != "").ToListAsync())
        {
            client.BusinessIds = Without(client.BusinessIds, id);
        }

        db.Businesses.Remove(business);
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "业务分组", $"{business.Code} {business.Name}", "删除业务分组");
        return Results.NoContent();
    }

    private static string Without(string ids, int id) =>
        string.Join(",", Auth.ApiKeyAuthenticator.ParseIds(ids).Where(item => item != id).Order());

    private static async Task<string?> ApplyAsync(LabelDbContext db, BusinessRecord business, BusinessRequest body)
    {
        var code = (body.Code ?? "").Trim().ToUpperInvariant();
        var name = (body.Name ?? "").Trim();
        if (!CodePattern().IsMatch(code))
        {
            return "编码要以大写字母开头，只含大写字母、数字、下划线，最长 32 位";
        }

        if (name.Length is 0 or > 64)
        {
            return "名称不能为空，最长 64 字";
        }

        if (await db.Businesses.AnyAsync(item => item.Code == code && item.Id != business.Id))
        {
            return $"编码 {code} 已被别的业务分组使用";
        }

        if (body.OwnerUserId is { } owner && !await db.Users.AnyAsync(item => item.Id == owner))
        {
            return "负责人不存在";
        }

        business.Code = code;
        business.Name = name;
        business.Description = (body.Description ?? "").Trim();
        business.OwnerUserId = body.OwnerUserId;
        business.Color = ColorPattern().IsMatch(body.Color ?? "") ? body.Color! : business.Color;
        return null;
    }

    private static object View(BusinessRecord business, int templates) => new
    {
        id = business.Id,
        code = business.Code,
        name = business.Name,
        description = business.Description,
        ownerUserId = business.OwnerUserId,
        color = business.Color,
        templates,
    };

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();

    /// <summary>新建、修改业务分组。</summary>
    internal sealed record BusinessRequest(string? Code, string? Name, string? Description, int? OwnerUserId, string? Color);
}
