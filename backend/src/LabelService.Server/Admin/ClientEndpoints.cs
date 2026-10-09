using LabelService.Server.Auth;
using LabelService.Server.Data;

namespace LabelService.Server.Admin;

/// <summary>
/// 调用方与密钥（api.manage）：新建（返回一次 Key）、修改业务范围和 IP 白名单、启停、重置 Key。
/// </summary>
internal static class ClientEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/clients", ListAsync).RequireAdmin(Permissions.ApiManage);
        api.MapPost("/clients", CreateAsync).RequireAdmin(Permissions.ApiManage);
        api.MapPut("/clients/{id:int}", UpdateAsync).RequireAdmin(Permissions.ApiManage);
        api.MapPost("/clients/{id:int}/status", SetStatusAsync).RequireAdmin(Permissions.ApiManage);
        api.MapPost("/clients/{id:int}/reset-key", ResetKeyAsync).RequireAdmin(Permissions.ApiManage);
    }

    private static async Task<IResult> ListAsync(ApiClientRepository repository) =>
        Results.Json((await repository.ListAsync()).Select(View));

    private static async Task<IResult> CreateAsync(ClientRequest body, HttpContext http, ApiClientRepository repository, OperationLog log)
    {
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
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }
    }

    private static async Task<IResult> UpdateAsync(int id, ClientRequest body, HttpContext http, ApiClientRepository repository, OperationLog log)
    {
        try
        {
            var client = await repository.UpdateAsync(id, body.ToInput(), null);
            await log.WriteAsync(http, "调用方", client.Name, "修改了名称、业务范围或 IP 白名单");
            return Results.Json(View(client));
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }
    }

    private static async Task<IResult> SetStatusAsync(int id, UserRoleEndpoints.StatusRequest body, HttpContext http, ApiClientRepository repository, OperationLog log)
    {
        try
        {
            var client = await repository.UpdateAsync(id, null, body.Enabled);
            await log.WriteAsync(http, "调用方", client.Name, body.Enabled ? "启用" : "停用（立即不能调用）");
            return Results.Json(View(client));
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }
    }

    private static async Task<IResult> ResetKeyAsync(int id, HttpContext http, ApiClientRepository repository, OperationLog log)
    {
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
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }
    }

    /// <summary>
    /// 调用方的信息。不含 Key 哈希。
    /// </summary>
    private static object View(ApiClientRecord client) => new
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
