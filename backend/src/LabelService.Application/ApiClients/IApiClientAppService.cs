using LabelService.Application.ApiClients.Dtos;

namespace LabelService.Application.ApiClients;

/// <summary>
/// 调用方与密钥（权限点 api.manage）：新建（返回一次 Key）、修改业务范围和 IP 白名单、启停、重置 Key。
/// </summary>
/// <remarks>
/// 业务范围（ADR 0006）：只能管理授权完全在自己范围里的调用方，也只能把自己范围里的分组授权出去；
/// 不是“全部业务”的用户不能授权全部业务。否则范围受限的用户新建或重置一把全部业务的 Key，就能从开放接口调到别的业务的模板。
/// 每次变更后重新加载 <see cref="ApiKeyAuthenticator"/>，停用、重置立即生效。
/// </remarks>
public interface IApiClientAppService
{
    /// <summary>
    /// 当前用户能管理的调用方，按名称排序。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>调用方列表。</returns>
    Task<IReadOnlyList<ApiClientDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新建调用方，生成 Key。
    /// </summary>
    /// <param name="input">名称、AppId、说明、业务范围、IP 白名单。</param>
    /// <returns>新调用方和 Key 原文。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">授权超出自己的业务范围（FORBIDDEN）、内容不合法。</exception>
    Task<ApiClientKeyDto> CreateAsync(SaveApiClientDto input);

    /// <summary>
    /// 修改名称、AppId、说明、业务范围、IP 白名单。
    /// </summary>
    /// <param name="id">调用方 ID。</param>
    /// <param name="input">新的内容。</param>
    /// <returns>修改后的调用方。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或管不了（NOT_FOUND）、授权超出业务范围（FORBIDDEN）、内容不合法。</exception>
    Task<ApiClientDto> UpdateAsync(int id, SaveApiClientDto input);

    /// <summary>
    /// 启用或停用调用方。停用后立即不能调用。
    /// </summary>
    /// <param name="id">调用方 ID。</param>
    /// <param name="enabled">true 启用，false 停用。</param>
    /// <returns>修改后的调用方。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或管不了（NOT_FOUND）。</exception>
    Task<ApiClientDto> SetStatusAsync(int id, bool enabled);

    /// <summary>
    /// 重置 Key：生成新 Key，旧 Key 立即失效。
    /// </summary>
    /// <param name="id">调用方 ID。</param>
    /// <returns>调用方和新 Key 原文。</returns>
    /// <exception cref="Domain.Shared.UserFriendlyException">不存在或管不了（NOT_FOUND）。</exception>
    Task<ApiClientKeyDto> ResetKeyAsync(int id);

    /// <summary>
    /// 从数据库重新加载调用方到 <see cref="ApiKeyAuthenticator"/>（启动时调用）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载完成时完成。</returns>
    Task ReloadAuthenticatorAsync(CancellationToken cancellationToken = default);
}
