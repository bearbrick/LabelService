using LabelService.Application.Layouts;
using LabelService.Application.Templates;
using LabelService.Domain.Businesses;
using LabelService.Domain.Layouts;
using LabelService.Domain.Shared;
using LabelService.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelService.Infrastructure.Hosting;

/// <summary>
/// 开发环境首次建库时导入示例模板和示例版式，走正常的保存草稿、发布、保存版式流程。
/// </summary>
internal static class SampleImporter
{
    /// <summary>
    /// 示例模板所在的业务分组编码。
    /// </summary>
    public const string SampleBusinessCode = "SAMPLE";

    /// <summary>
    /// 导入的操作人。
    /// </summary>
    private const string SystemUser = "system";

    /// <summary>
    /// 把示例模板导入为已发布版本，放在业务分组“示例”（SAMPLE）里。发布检查不通过的只保存草稿，记警告。
    /// </summary>
    /// <param name="services">根服务（每个模板用一个新作用域，前一个失败不影响后一个）。</param>
    /// <param name="directory">示例模板目录。</param>
    /// <param name="logger">日志。</param>
    /// <returns>完成时完成。</returns>
    public static async Task ImportTemplatesAsync(IServiceProvider services, string directory, ILogger logger)
    {
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("示例模板目录 {Directory} 不存在，跳过导入", directory);
            return;
        }

        var businessId = await EnsureSampleBusinessAsync(services);
        foreach (var template in SeedFiles.ReadTemplates(directory))
        {
            await using var scope = services.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<TemplateManager>();
            try
            {
                await manager.SaveDraftAsync(template, SystemUser, businessId);
                await manager.PublishAsync(template.Code, SystemUser);
                logger.LogInformation("已导入示例模板 {Code}", template.Code);
            }
            catch (UserFriendlyException ex)
            {
                logger.LogWarning("示例模板 {Code} 没有发布：{Message} {Details}", template.Code, ex.Message, string.Join("；", ex.Details));
            }
        }
    }

    /// <summary>
    /// 版式表是空的时候导入示例版式（启用）。检查不通过的跳过，记警告。
    /// </summary>
    /// <param name="services">根服务。</param>
    /// <param name="directory">示例版式目录。</param>
    /// <param name="logger">日志。</param>
    /// <returns>完成时完成。</returns>
    public static async Task ImportLayoutsAsync(IServiceProvider services, string directory, ILogger logger)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        await using (var check = services.CreateAsyncScope())
        {
            if (await check.ServiceProvider.GetRequiredService<ISheetLayoutRepository>().AnyAsync())
            {
                return;
            }
        }

        foreach (var layout in SeedFiles.ReadLayouts(directory))
        {
            await using var scope = services.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<SheetLayoutManager>().SaveAsync(layout, enabled: true, SystemUser, create: true);
                logger.LogInformation("已导入示例版式 {Code}", layout.Code);
            }
            catch (UserFriendlyException ex)
            {
                logger.LogWarning("示例版式 {Code} 没有导入：{Message} {Details}", layout.Code, ex.Message, string.Join("；", ex.Details));
            }
        }
    }

    /// <summary>
    /// 找到或新建业务分组“示例”。
    /// </summary>
    /// <param name="services">根服务。</param>
    /// <returns>业务分组 ID。</returns>
    private static async Task<int> EnsureSampleBusinessAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var businesses = scope.ServiceProvider.GetRequiredService<IBusinessRepository>();
        var business = await businesses.FindByCodeAsync(SampleBusinessCode);
        if (business is null)
        {
            business = new BusinessEntity { Code = SampleBusinessCode, Name = "示例", Description = "开发环境自动导入的示例模板", CreatedAt = DateTime.UtcNow };
            businesses.Add(business);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        return business.Id;
    }
}
