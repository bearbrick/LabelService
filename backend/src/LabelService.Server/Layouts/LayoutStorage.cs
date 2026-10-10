using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Layouts;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Layouts;

/// <summary>
/// 版式存储的注册和启动加载，由 <see cref="Templates.TemplateStorage"/> 一起调用（和模板用同一个数据库、同一种来源配置）。
/// </summary>
internal static class LayoutStorage
{
    /// <summary>
    /// 注册版式存储。
    /// </summary>
    public static IServiceCollection AddLayoutStorage(this IServiceCollection services)
    {
        services.AddSingleton<LayoutStore>();
        services.AddSingleton<LayoutRepository>();
        return services;
    }

    /// <summary>
    /// 数据库来源：开发环境表是空的时候导入示例版式，再把启用的版式加载进内存。数据库迁移已经由模板存储执行过。
    /// </summary>
    public static async Task InitializeDatabaseAsync(IServiceProvider services, bool importSamples, string seedDirectory, ILogger logger)
    {
        var repository = services.GetRequiredService<LayoutRepository>();
        if (importSamples && Directory.Exists(seedDirectory))
        {
            bool empty;
            await using (var db = await services.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContextAsync())
            {
                empty = !await db.SheetLayouts.AnyAsync();
            }

            if (empty)
            {
                foreach (var layout in ReadDirectory(seedDirectory))
                {
                    try
                    {
                        await repository.SaveAsync(layout, enabled: true, "system", create: true);
                        logger.LogInformation("已导入示例版式 {Code}", layout.Code);
                    }
                    catch (UserFriendlyException ex)
                    {
                        logger.LogWarning("示例版式 {Code} 没有导入：{Message} {Details}", layout.Code, ex.Message, string.Join("；", ex.Details));
                    }
                }
            }
        }

        var count = await repository.LoadAsync();
        logger.LogInformation("拼版版式：已加载 {Count} 个启用的版式", count);
    }

    /// <summary>
    /// 文件来源：版式目录里的 JSON 全部当作启用的版式，检查不通过的跳过并记录警告。
    /// </summary>
    public static void LoadFiles(IServiceProvider services, string directory, ILogger logger)
    {
        var layouts = new List<SheetLayout>();
        if (Directory.Exists(directory))
        {
            foreach (var layout in ReadDirectory(directory).Select(LayoutRepository.Normalize))
            {
                var problems = LayoutValidator.Validate(layout);
                if (problems.Count > 0)
                {
                    logger.LogWarning("版式文件 {Code} 检查不通过，跳过：{Problems}", layout.Code, string.Join("；", problems));
                    continue;
                }

                layouts.Add(layout);
            }
        }

        services.GetRequiredService<LayoutStore>().Load(layouts);
        logger.LogInformation("拼版版式：从 {Directory} 加载 {Count} 个", directory, layouts.Count);
    }

    /// <summary>
    /// 读取目录里的全部版式 JSON，按文件名排序。
    /// </summary>
    public static IEnumerable<SheetLayout> ReadDirectory(string directory) =>
        Directory.EnumerateFiles(directory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<SheetLayout>(File.ReadAllText(path), LabelJson.Options)
                ?? throw new InvalidDataException($"版式文件为空：{path}"));
}
