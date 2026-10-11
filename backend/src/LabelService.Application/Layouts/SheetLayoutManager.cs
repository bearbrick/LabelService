using LabelService.Contracts.Layouts;
using LabelService.Domain.Layouts;
using LabelService.Domain.Shared;

namespace LabelService.Application.Layouts;

/// <summary>
/// 版式的核心流程：保存、删除和启动加载。改动保存后立即更新 <see cref="SheetLayoutCache"/>，开放接口马上能用。
/// 不做权限检查，启动导入示例版式时也用它。
/// </summary>
/// <param name="layouts">版式仓储。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="cache">版式的内存缓存。</param>
/// <param name="clock">时间来源。</param>
public sealed class SheetLayoutManager(ISheetLayoutRepository layouts, IUnitOfWork unitOfWork, SheetLayoutCache cache, TimeProvider clock)
{
    /// <summary>
    /// 新建或修改版式。长度按 0.1 mm 取整后再检查。
    /// </summary>
    /// <param name="layout">版式内容。修改时编码不能变。</param>
    /// <param name="enabled">是否启用。</param>
    /// <param name="account">操作人账号。</param>
    /// <param name="create">true 新建（编码已存在时报错），false 修改（不存在时返回 null）。</param>
    /// <returns>保存后的版式记录；修改时版式不存在为 null。</returns>
    /// <exception cref="UserFriendlyException">检查不通过（<see cref="UserFriendlyException.Details"/> 列出全部问题）、新建时编码已存在。</exception>
    public async Task<SheetLayoutEntity?> SaveAsync(SheetLayout layout, bool enabled, string account, bool create)
    {
        var normalized = SheetLayoutRules.Normalize(layout);
        var problems = SheetLayoutRules.Validate(normalized);
        if (problems.Count > 0)
        {
            throw new UserFriendlyException("版式检查不通过", problems);
        }

        var entity = await layouts.FindForUpdateAsync(normalized.Code);
        if (create && entity is not null)
        {
            throw new UserFriendlyException($"版式编码 {normalized.Code} 已存在");
        }

        if (!create && entity is null)
        {
            return null;
        }

        if (entity is null)
        {
            entity = new SheetLayoutEntity { Code = normalized.Code };
            layouts.Add(entity);
        }

        SheetLayoutMapping.Apply(entity, normalized);
        entity.Enabled = enabled;
        entity.UpdatedBy = account;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync();

        // 写库成功后更新缓存：停用的从缓存里去掉。
        cache.Set(normalized, enabled);
        return entity;
    }

    /// <summary>
    /// 删除版式。已生成的文件不受影响，之后带这个编码的请求返回 LAYOUT_NOT_FOUND。
    /// </summary>
    /// <param name="code">版式编码。</param>
    /// <returns>删除了为 true，不存在为 false。</returns>
    public async Task<bool> DeleteAsync(string code)
    {
        var entity = await layouts.FindForUpdateAsync(code);
        if (entity is null)
        {
            return false;
        }

        layouts.Remove(entity);
        await unitOfWork.SaveChangesAsync();
        cache.Remove(code);
        return true;
    }

    /// <summary>
    /// 把全部启用的版式加载进内存缓存。启动时调用一次。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载的版式数。</returns>
    public async Task<int> LoadAsync(CancellationToken cancellationToken = default)
    {
        var enabled = await layouts.ListEnabledAsync(cancellationToken);
        cache.Load(enabled.Select(SheetLayoutMapping.ToLayout));
        return enabled.Count;
    }
}
