using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using LabelService.Server.Auth;

namespace LabelService.Server.Admin;

/// <summary>
/// 调用方与密钥（api.manage）：新建（返回一次 Key）、修改业务范围和 IP 白名单、启停、重置 Key。
/// </summary>
/// <remarks>
/// 业务范围（ADR 0006，T-021）：只能管理授权完全在自己范围里的调用方，也只能把自己范围里的分组授权出去；
/// 不是“全部业务”的用户不能授权全部业务。否则范围受限的用户新建或重置一把全部业务的 Key，就能从开放接口调到别的业务的模板。
/// </remarks>
internal static class ClientEndpoints
{
    private const string OutOfScope = "只能把自己业务范围里的分组授权给调用方";

    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/clients", ListAsync).RequireAdmin(PermissionNames.ApiManage);
        api.MapPost("/clients", CreateAsync).RequireAdmin(PermissionNames.ApiManage);
        api.MapPut("/clients/{id:int}", UpdateAsync).RequireAdmin(PermissionNames.ApiManage);
        api.MapPost("/clients/{id:int}/status", SetStatusAsync).RequireAdmin(PermissionNames.ApiManage);
        api.MapPost("/clients/{id:int}/reset-key", ResetKeyAsync).RequireAdmin(PermissionNames.ApiManage);
    }

    private static async Task<IResult> ListAsync(HttpContext http, ApiClientRepository repository)
    {
        var admin = http.CurrentAdmin();
        return Results.Json((await repository.ListAsync()).Where(client => Manages(admin, client)).Select(View));
    }

    private static async Task<IResult> CreateAsync(ClientRequest body, HttpContext http, ApiClientRepository repository, OperationLog log)
    {
        if (!Grants(http.CurrentAdmin(), body))
        {
            return AdminResults.Forbidden(OutOfScope);
        }

        try
        {
            var (client, key) = await repository.CreateAsync(body.ToInput());
            await log.WriteAsync(http, "调用方", client.Name, $"新建调用方，生成 Key {client.KeyPrefix}…");
            return Results.Json(new
            {
                client = View(client),
                apiKey = key
            });
        }
        catch (UserFriendlyException ex)
        {
            return AdminResults.From(ex);
        }
    }

    private static async Task<IResult> UpdateAsync(int id, ClientRequest body, HttpContext http, ApiClientRepository repository, OperationLog log)
    {
        if (await CheckScopeAsync(id, http, repository) is { } denied)
        {
            return denied;
        }

        if (!Grants(http.CurrentAdmin(), body))
        {
            return AdminResults.Forbidden(OutOfScope);
        }

        try
        {
            var client = await repository.UpdateAsync(id, body.ToInput(), null);
            await log.WriteAsync(http, "调用方", client.Name, "修改了名称、业务范围或 IP 白名单");
            return Results.Json(View(client));
        }
        catch (UserFriendlyException ex)
        {
            return AdminResults.From(ex);
        }
    }

    private static async Task<IResult> SetStatusAsync(int id, UserRoleEndpoints.StatusRequest body, HttpContext http, ApiClientRepository repository, OperationLog log)
    {
        if (await CheckScopeAsync(id, http, repository) is { } denied)
        {
            return denied;
        }

        try
        {
            var client = await repository.UpdateAsync(id, null, body.Enabled);
            await log.WriteAsync(http, "调用方", client.Name, body.Enabled ? "启用" : "停用（立即不能调用）");
            return Results.Json(View(client));
        }
        catch (UserFriendlyException ex)
        {
            return AdminResults.From(ex);
        }
    }

    private static async Task<IResult> ResetKeyAsync(int id, HttpContext http, ApiClientRepository repository, OperationLog log)
    {
        if (await CheckScopeAsync(id, http, repository) is { } denied)
        {
            return denied;
        }

        try
        {
            var (client, key) = await repository.ResetKeyAsync(id);
            await log.WriteAsync(http, "调用方", client.Name, $"重置 Key，旧 Key 立即失效，新 Key {client.KeyPrefix}…");
            return Results.Json(new
            {
                client = View(client),
                apiKey = key
            });
        }
        catch (UserFriendlyException ex)
        {
            return AdminResults.From(ex);
        }
    }

    /// <summary>
    /// 调用方不存在或授权超出自己的业务范围时返回 404（和模板一样不暴露是否存在），否则返回 null。
    /// </summary>
    private static async Task<IResult?> CheckScopeAsync(int id, HttpContext http, ApiClientRepository repository) =>
        await repository.FindAsync(id) is { } client && Manages(http.CurrentAdmin(), client) ? null : AdminResults.NotFound("调用方不存在");

    /// <summary>
    /// 调用方的授权是否完全在用户的业务范围里。没有授权任何分组的调用方谁都能管。
    /// </summary>
    private static bool Manages(AdminUser admin, ApiClientEntity client) =>
        admin.AllBusinesses || (!client.AllBusinesses && ApiKeyAuthenticator.ParseIds(client.BusinessIds).All(id => admin.InScope(id)));

    /// <summary>
    /// 要授权的分组是否都在用户的业务范围里。
    /// </summary>
    private static bool Grants(AdminUser admin, ClientRequest body) =>
        admin.AllBusinesses || (!body.AllBusinesses && (body.BusinessIds ?? []).All(id => admin.InScope(id)));

    /// <summary>
    /// 调用方的信息。不含 Key 哈希。
    /// </summary>
    private static object View(ApiClientEntity client) => new
    {
        id = client.Id,
        name = client.Name,
        appId = client.AppId,
        description = client.Description,
        keyPrefix = client.KeyPrefix,
        allBusinesses = client.AllBusinesses,
        businessIds = ApiKeyAuthenticator.ParseIds(client.BusinessIds).Order().ToArray(),
        ipAllowlist = client.IpAllowlist,
        enabled = client.Enabled,
        createdAt = client.CreatedAt,
        updatedAt = client.UpdatedAt,
    };

    /// <summary>新建、修改调用方。</summary>
    internal sealed record ClientRequest(string? Name, string? AppId, string? Description, bool AllBusinesses, int[]? BusinessIds, string? IpAllowlist)
    {
        /// <summary>转成仓库的输入。</summary>
        public ApiClientInput ToInput() => new(Name ?? "", AppId ?? "", Description ?? "", AllBusinesses, BusinessIds ?? [], IpAllowlist ?? "");
    }
}
