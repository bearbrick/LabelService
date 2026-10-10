using LabelService.Application.Templates;
using LabelService.Application.Templates.Dtos;
using LabelService.Contracts.Templates;
using LabelService.Domain.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace LabelService.Backend.Checks;

/// <summary>
/// 检查程序直接调用模板核心流程（<see cref="TemplateManager"/>）时用：每次调用一个新作用域，和一次请求一样。
/// </summary>
/// <param name="services">服务的根容器（<c>factory.Services</c>）。</param>
internal sealed class ScopedTemplateManager(IServiceProvider services)
{
    /// <summary>
    /// 模板列表（不含已删除的）。
    /// </summary>
    /// <returns>每个模板的概要。</returns>
    public Task<IReadOnlyList<TemplateSummary>> ListAsync() => RunAsync<ITemplateRepository, IReadOnlyList<TemplateSummary>>(repository => repository.ListAsync());

    /// <summary>
    /// 保存草稿。
    /// </summary>
    /// <param name="content">模板内容。</param>
    /// <param name="user">操作人。</param>
    /// <returns>草稿的版本号。</returns>
    public Task<int> SaveDraftAsync(LabelTemplate content, string user) => RunAsync<TemplateManager, int>(manager => manager.SaveDraftAsync(content, user));

    /// <summary>
    /// 发布草稿。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="user">操作人。</param>
    /// <returns>发布结果。</returns>
    public Task<PublishResultDto> PublishAsync(string code, string user) => RunAsync<TemplateManager, PublishResultDto>(manager => manager.PublishAsync(code, user));

    /// <summary>
    /// 取草稿内容。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>草稿；没有时为 null。</returns>
    public Task<LabelTemplate?> GetDraftAsync(string code) => RunAsync<TemplateManager, LabelTemplate?>(manager => manager.GetDraftAsync(code));

    /// <summary>
    /// 把已发布版本恢复成草稿。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="versionNo">版本号。</param>
    /// <param name="user">操作人。</param>
    /// <returns>草稿的版本号。</returns>
    public Task<int> RestoreToDraftAsync(string code, int versionNo, string user) =>
        RunAsync<TemplateManager, int>(manager => manager.RestoreToDraftAsync(code, versionNo, user));

    /// <summary>
    /// 放弃草稿。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <returns>有草稿并已删除时为 true。</returns>
    public Task<bool> DiscardDraftAsync(string code) => RunAsync<TemplateManager, bool>(manager => manager.DiscardDraftAsync(code));

    /// <summary>
    /// 在一个新作用域里调用一次。
    /// </summary>
    /// <typeparam name="TService">要用的服务。</typeparam>
    /// <typeparam name="TResult">返回值类型。</typeparam>
    /// <param name="action">要做的调用。</param>
    /// <returns>调用的返回值。</returns>
    private async Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        await using var scope = services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
