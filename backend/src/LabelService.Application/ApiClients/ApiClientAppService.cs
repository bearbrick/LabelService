using LabelService.Application.ApiClients.Dtos;
using LabelService.Application.Identity;
using LabelService.Application.Logs;
using LabelService.Application.Shared;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Shared;
using Microsoft.Extensions.Hosting;

namespace LabelService.Application.ApiClients;

/// <summary>
/// 调用方与密钥的应用服务实现。
/// </summary>
/// <param name="clients">调用方仓储。</param>
/// <param name="businesses">业务分组仓储。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="authenticator">开放接口的 Key 鉴权器（内存快照）。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="currentAdmin">当前用户。</param>
/// <param name="environment">运行环境：正式环境的 Key 以 lbl_live_ 开头。</param>
/// <param name="clock">时间来源。</param>
public sealed class ApiClientAppService(
    IApiClientRepository clients,
    IBusinessRepository businesses,
    IUnitOfWork unitOfWork,
    ApiKeyAuthenticator authenticator,
    IOperationLogger operationLog,
    ICurrentAdmin currentAdmin,
    IHostEnvironment environment,
    TimeProvider clock) : IApiClientAppService
{
    /// <summary>
    /// 授权超出自己业务范围时的说明。
    /// </summary>
    private const string OutOfScope = "只能把自己业务范围里的分组授权给调用方";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiClientDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var scope = currentAdmin.Principal.Scope;
        return (await clients.ListAsync(cancellationToken)).Where(client => Manages(scope, client)).Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ApiClientKeyDto> CreateAsync(SaveApiClientDto input)
    {
        EnsureGrants(input);
        var key = ApiKeys.Generate(environment.IsProduction());
        var now = clock.GetUtcNow().UtcDateTime;
        var client = new ApiClientEntity { ApiKeyHash = ApiKeys.Hash(key), KeyPrefix = ApiKeys.Prefix(key), Enabled = true, CreatedAt = now, UpdatedAt = now };
        await ApplyAsync(client, input);
        clients.Add(client);
        await unitOfWork.SaveChangesAsync();
        await ReloadAuthenticatorAsync();
        await operationLog.WriteAsync("调用方", client.Name, $"新建调用方，生成 Key {client.KeyPrefix}…");
        return new ApiClientKeyDto(ToDto(client), key);
    }

    /// <inheritdoc />
    public async Task<ApiClientDto> UpdateAsync(int id, SaveApiClientDto input)
    {
        // 先判断能不能管（404），再判断新的授权是否超出范围（403）。
        var client = await FindManagedAsync(id);
        EnsureGrants(input);
        await ApplyAsync(client, input);
        client.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();
        await ReloadAuthenticatorAsync();
        await operationLog.WriteAsync("调用方", client.Name, "修改了名称、业务范围或 IP 白名单");
        return ToDto(client);
    }

    /// <inheritdoc />
    public async Task<ApiClientDto> SetStatusAsync(int id, bool enabled)
    {
        var client = await FindManagedAsync(id);
        client.Enabled = enabled;
        client.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();
        await ReloadAuthenticatorAsync();
        await operationLog.WriteAsync("调用方", client.Name, enabled ? "启用" : "停用（立即不能调用）");
        return ToDto(client);
    }

    /// <inheritdoc />
    public async Task<ApiClientKeyDto> ResetKeyAsync(int id)
    {
        var client = await FindManagedAsync(id);
        var key = ApiKeys.Generate(environment.IsProduction());
        client.ApiKeyHash = ApiKeys.Hash(key);
        client.KeyPrefix = ApiKeys.Prefix(key);
        client.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();
        await ReloadAuthenticatorAsync();
        await operationLog.WriteAsync("调用方", client.Name, $"重置 Key，旧 Key 立即失效，新 Key {client.KeyPrefix}…");
        return new ApiClientKeyDto(ToDto(client), key);
    }

    /// <inheritdoc />
    public async Task ReloadAuthenticatorAsync(CancellationToken cancellationToken = default) =>
        authenticator.Load(await clients.ListAsync(cancellationToken));

    /// <summary>
    /// 调用方的授权是否完全在业务范围里。没有授权任何分组的调用方谁都能管。
    /// </summary>
    /// <param name="scope">当前用户的业务范围。</param>
    /// <param name="client">调用方。</param>
    /// <returns>能管理时为 true。</returns>
    private static bool Manages(BusinessScope scope, ApiClientEntity client) =>
        scope.Covers(client.AllBusinesses, IdList.Parse(client.BusinessIds));

    /// <summary>
    /// 转成接口返回的调用方。
    /// </summary>
    /// <param name="client">调用方实体。</param>
    /// <returns>调用方 DTO，业务分组 ID 从小到大。</returns>
    private static ApiClientDto ToDto(ApiClientEntity client) => new(
        client.Id,
        client.Name,
        client.AppId,
        client.Description,
        client.KeyPrefix,
        client.AllBusinesses,
        IdList.Parse(client.BusinessIds).Order().ToArray(),
        client.IpAllowlist,
        client.Enabled,
        client.CreatedAt,
        client.UpdatedAt);

    /// <summary>
    /// 要授权的分组必须都在当前用户的业务范围里。
    /// </summary>
    /// <param name="input">请求内容。</param>
    /// <exception cref="UserFriendlyException">超出范围（FORBIDDEN）。</exception>
    private void EnsureGrants(SaveApiClientDto input)
    {
        if (!currentAdmin.Principal.Scope.Covers(input.AllBusinesses, input.BusinessIds ?? []))
        {
            throw AdminErrors.ForbiddenError(OutOfScope);
        }
    }

    /// <summary>
    /// 按 ID 取当前用户能管理的调用方（跟踪）。
    /// </summary>
    /// <param name="id">调用方 ID。</param>
    /// <returns>调用方。</returns>
    /// <exception cref="UserFriendlyException">不存在或授权超出范围（NOT_FOUND，和模板一样不暴露是否存在）。</exception>
    private async Task<ApiClientEntity> FindManagedAsync(int id)
    {
        var client = await clients.FindForUpdateAsync(id);
        return client is not null && Manages(currentAdmin.Principal.Scope, client) ? client : throw AdminErrors.NotFoundError("调用方不存在");
    }

    /// <summary>
    /// 校验并写入名称、AppId、说明、业务范围和 IP 白名单（新建、修改共用）。
    /// </summary>
    /// <param name="client">要写入的调用方。</param>
    /// <param name="input">请求内容。</param>
    /// <returns>写入完成时完成。</returns>
    /// <exception cref="UserFriendlyException">名称为空、太长或重复，AppId 重复，IP 白名单格式不对，业务分组不存在。</exception>
    private async Task ApplyAsync(ApiClientEntity client, SaveApiClientDto input)
    {
        var name = (input.Name ?? "").Trim();
        if (name.Length is 0 or > 64)
        {
            throw new UserFriendlyException("调用方名称不能为空，最长 64 个字符");
        }

        if (await clients.NameExistsAsync(name, client.Id))
        {
            throw new UserFriendlyException($"调用方 {name} 已存在");
        }

        var appId = (input.AppId ?? "").Trim();
        if (appId.Length > 0 && await clients.AppIdExistsAsync(appId, client.Id))
        {
            throw new UserFriendlyException($"AppId {appId} 已被别的调用方使用");
        }

        // 先规范化（中英文逗号、换行、空格都算分隔符）再校验，存的就是校验过的值。
        var allowlist = IpAllowlist.Normalize(input.IpAllowlist ?? "");
        try
        {
            IpAllowlist.Parse(allowlist);
        }
        catch (FormatException ex)
        {
            throw new UserFriendlyException("IP 白名单格式不对：" + ex.Message);
        }

        var selected = (input.BusinessIds ?? []).Distinct().ToList();
        if (!input.AllBusinesses && !await businesses.AllExistAsync(selected))
        {
            throw new UserFriendlyException("选择的业务分组有的不存在");
        }

        client.Name = name;
        client.AppId = appId;
        client.Description = (input.Description ?? "").Trim();
        client.AllBusinesses = input.AllBusinesses;
        client.BusinessIds = input.AllBusinesses ? "" : IdList.Format(selected);
        client.IpAllowlist = allowlist;
    }
}
