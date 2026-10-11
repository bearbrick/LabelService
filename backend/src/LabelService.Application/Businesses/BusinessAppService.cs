using LabelService.Application.Businesses.Dtos;
using LabelService.Application.Identity;
using LabelService.Application.Logs;
using LabelService.Application.Shared;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;

namespace LabelService.Application.Businesses;

/// <summary>
/// 业务分组的应用服务实现。
/// </summary>
/// <param name="businesses">业务分组仓储。</param>
/// <param name="templates">模板仓储（统计模板数、解除关联）。</param>
/// <param name="users">用户仓储（删除分组时修改业务范围）。</param>
/// <param name="clients">调用方仓储（删除分组时修改业务范围）。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="currentAdmin">当前用户。</param>
/// <param name="clock">时间来源。</param>
public sealed class BusinessAppService(
    IBusinessRepository businesses,
    ITemplateRepository templates,
    IUserRepository users,
    IApiClientRepository clients,
    IUnitOfWork unitOfWork,
    IOperationLogger operationLog,
    ICurrentAdmin currentAdmin,
    TimeProvider clock) : IBusinessAppService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<BusinessDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var admin = currentAdmin.Principal;
        var list = await businesses.ListAsync(cancellationToken);
        var counts = await templates.CountByBusinessAsync(cancellationToken);
        return list.Where(item => admin.InScope(item.Id)).Select(item => ToDto(item, counts.GetValueOrDefault(item.Id))).ToList();
    }

    /// <inheritdoc />
    public async Task<BusinessDto> CreateAsync(SaveBusinessDto input)
    {
        // 新分组排在最后：排序号按 10 递增，留出插队的空间。
        var business = new BusinessEntity { CreatedAt = clock.GetUtcNow().UtcDateTime, Sort = await businesses.CountAsync() * 10 + 10 };
        await ApplyAsync(business, input);
        businesses.Add(business);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("业务分组", Describe(business), "新建业务分组");
        return ToDto(business, 0);
    }

    /// <inheritdoc />
    public async Task<BusinessDto> UpdateAsync(int id, SaveBusinessDto input)
    {
        var business = await FindInScopeAsync(id);
        await ApplyAsync(business, input);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("业务分组", Describe(business), "修改业务分组");
        var counts = await templates.CountByBusinessAsync();
        return ToDto(business, counts.GetValueOrDefault(id));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id)
    {
        var business = await FindInScopeAsync(id);
        if (await templates.AnyInBusinessAsync(id))
        {
            throw new UserFriendlyException("分组里还有模板，先把模板移到别的分组或删除");
        }

        // 已删除的模板只留档，解除关联；用户和调用方的业务范围里去掉这个分组。
        await templates.DetachBusinessAsync(id);
        foreach (var user in await users.ListWithBusinessScopeAsync())
        {
            user.BusinessIds = IdList.Without(user.BusinessIds, id);
        }

        foreach (var client in await clients.ListWithBusinessScopeForUpdateAsync())
        {
            client.BusinessIds = IdList.Without(client.BusinessIds, id);
        }

        businesses.Remove(business);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("业务分组", Describe(business), "删除业务分组");
    }

    /// <summary>
    /// 转成接口返回的业务分组。
    /// </summary>
    /// <param name="business">业务分组实体。</param>
    /// <param name="templateCount">分组里的模板数。</param>
    /// <returns>业务分组 DTO。</returns>
    private static BusinessDto ToDto(BusinessEntity business, int templateCount) =>
        new(business.Id, business.Code, business.Name, business.Description, business.OwnerUserId, business.Color, templateCount);

    /// <summary>
    /// 操作日志里写的操作对象，如“WMS 仓储”。
    /// </summary>
    /// <param name="business">业务分组。</param>
    /// <returns>编码和名称。</returns>
    private static string Describe(BusinessEntity business) => $"{business.Code} {business.Name}";

    /// <summary>
    /// 按 ID 取当前用户业务范围里的分组（跟踪）。
    /// </summary>
    /// <param name="id">业务分组 ID。</param>
    /// <returns>业务分组。</returns>
    /// <exception cref="UserFriendlyException">不存在或不在范围里（NOT_FOUND，不暴露是否存在）。</exception>
    private async Task<BusinessEntity> FindInScopeAsync(int id)
    {
        var business = await businesses.FindAsync(id);
        return business is not null && currentAdmin.Principal.InScope(id) ? business : throw AdminErrors.NotFoundError("业务分组不存在");
    }

    /// <summary>
    /// 校验并写入编码、名称、说明、负责人和标识色（新建、修改共用）。
    /// </summary>
    /// <param name="business">要写入的分组。</param>
    /// <param name="input">请求内容。</param>
    /// <returns>写入完成时完成。</returns>
    /// <exception cref="UserFriendlyException">编码或名称不合法、编码重复、负责人不存在。</exception>
    private async Task ApplyAsync(BusinessEntity business, SaveBusinessDto input)
    {
        var code = BusinessRules.NormalizeCode(input.Code);
        var name = (input.Name ?? "").Trim();
        if (!BusinessRules.IsValidCode(code))
        {
            throw new UserFriendlyException(BusinessRules.CodeRule);
        }

        if (!BusinessRules.IsValidName(name))
        {
            throw new UserFriendlyException(BusinessRules.NameRule);
        }

        if (await businesses.CodeExistsAsync(code, business.Id))
        {
            throw new UserFriendlyException($"编码 {code} 已被别的业务分组使用");
        }

        if (input.OwnerUserId is { } owner && !await users.ExistsAsync(owner))
        {
            throw new UserFriendlyException("负责人不存在");
        }

        business.Code = code;
        business.Name = name;
        business.Description = (input.Description ?? "").Trim();
        business.OwnerUserId = input.OwnerUserId;

        // 颜色格式不对时保持原值（新建时是实体的默认色）。
        business.Color = BusinessRules.IsValidColor(input.Color) ? input.Color! : business.Color;
    }
}
