using LabelService.Application.ApiClients;
using LabelService.Application.Identity;
using LabelService.Application.Layouts;
using LabelService.Application.Logs;
using LabelService.Application.Templates;
using LabelService.Contracts.Layouts;
using LabelService.Domain.Layouts;
using LabelService.Infrastructure.Persistence;
using LabelService.Infrastructure.Storage;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Fonts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure.Hosting;

/// <summary>
/// 启动初始化：在开始接收请求之前调用一次。
/// </summary>
/// <remarks>
/// <para>模板来源是数据库（默认）时：执行主库和日志库的迁移，建内置角色和 admin，开发环境首次建库时导入示例，
/// 再把已发布模板、启用的版式、调用方加载进内存缓存。</para>
/// <para>模板来源是文件时：从模板目录、版式目录加载，不用数据库，也不写调用日志。</para>
/// <para>字体和素材在这里加载，避免第一次请求时才加载。</para>
/// </remarks>
public static class StartupInitializer
{
    /// <summary>
    /// 执行启动初始化。
    /// </summary>
    /// <param name="services">根服务。</param>
    /// <param name="environment">运行环境：开发环境导入示例、用固定的 admin 初始密码。</param>
    /// <returns>完成时完成。</returns>
    public static async Task InitializeAsync(IServiceProvider services, IHostEnvironment environment)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(StartupInitializer));
        services.GetRequiredService<FontRegistry>();
        services.GetRequiredService<AssetLibrary>();
        var options = services.GetRequiredService<IOptions<TemplateStoreOptions>>().Value;
        if (options.Source == TemplateSource.Files)
        {
            LoadTemplateFiles(services, options.ResolveSeedDirectory(), logger);
            LoadLayoutFiles(services, options.ResolveSeedLayoutDirectory(), logger);
            services.GetRequiredService<RenderLogQueue>().Enabled = false;
            return;
        }

        var directory = services.GetRequiredService<IOptions<DataOptions>>().Value.ResolveDirectory();
        Directory.CreateDirectory(directory);
        await UpgradeDatabasesAsync(services, directory, logger);

        // 读一次签名密钥：没有配置时生成到数据目录，启动时就准备好。
        services.GetRequiredService<IAdminTokenService>();
        await using (var scope = services.CreateAsyncScope())
        {
            await IdentitySeeder.SeedAsync(scope.ServiceProvider, environment, logger);
        }

        await LoadDatabaseAsync(services, environment, options, directory, logger);
    }

    /// <summary>
    /// 主库迁移；调用日志开启时日志库迁移。SQLite 有待执行的迁移时先备份、失败恢复，并换算旧库的数据。
    /// </summary>
    /// <param name="services">根服务。</param>
    /// <param name="directory">数据目录。</param>
    /// <param name="logger">日志。</param>
    /// <returns>完成时完成。</returns>
    private static async Task UpgradeDatabasesAsync(IServiceProvider services, string directory, ILogger logger)
    {
        var main = services.GetRequiredService<IDbContextFactory<LabelDbContext>>();
        var log = services.GetRequiredService<IDbContextFactory<LogDbContext>>();
        var logEnabled = services.GetRequiredService<RenderLogQueue>().Enabled;
        if (!services.GetRequiredService<IOptions<DataOptions>>().Value.IsSqlite)
        {
            // SQL Server、PostgreSQL：迁移在事务里执行，失败自动回滚。
            await DatabaseUpgrade.MigrateAsync(() => main.CreateDbContextAsync(), "主库", logger);
            if (logEnabled)
            {
                await DatabaseUpgrade.MigrateAsync(() => log.CreateDbContextAsync(), "日志库", logger);
            }

            return;
        }

        // SQLite：升级前做快照，失败时恢复；旧库的数据换算。
        await DatabaseUpgrade.UpgradeMainAsync(() => main.CreateDbContextAsync(), directory, services.GetRequiredService<TimeProvider>(), logger);
        if (logEnabled)
        {
            await DatabaseUpgrade.UpgradeLogAsync(() => log.CreateDbContextAsync(), directory, logger);
        }
    }

    /// <summary>
    /// 开发环境首次建库时导入示例，再把已发布模板、启用的版式、调用方加载进内存。
    /// </summary>
    /// <param name="services">根服务。</param>
    /// <param name="environment">运行环境。</param>
    /// <param name="options">模板来源配置。</param>
    /// <param name="directory">数据目录（日志里显示）。</param>
    /// <param name="logger">日志。</param>
    /// <returns>完成时完成。</returns>
    private static async Task LoadDatabaseAsync(IServiceProvider services, IHostEnvironment environment, TemplateStoreOptions options, string directory, ILogger logger)
    {
        bool empty;
        await using (var scope = services.CreateAsyncScope())
        {
            empty = !await scope.ServiceProvider.GetRequiredService<Domain.Templates.ITemplateRepository>().AnyAsync();
        }

        if (empty && environment.IsDevelopment())
        {
            await SampleImporter.ImportTemplatesAsync(services, options.ResolveSeedDirectory(), logger);
        }

        if (environment.IsDevelopment())
        {
            await SampleImporter.ImportLayoutsAsync(services, options.ResolveSeedLayoutDirectory(), logger);
        }

        await using var load = services.CreateAsyncScope();
        var count = await load.ServiceProvider.GetRequiredService<TemplateManager>().LoadPublishedAsync();
        logger.LogInformation("模板数据库 {Directory}：已加载 {Count} 个已发布版本", directory, count);
        var layouts = await load.ServiceProvider.GetRequiredService<SheetLayoutManager>().LoadAsync();
        logger.LogInformation("拼版版式：已加载 {Count} 个启用的版式", layouts);
        await load.ServiceProvider.GetRequiredService<IApiClientAppService>().ReloadAuthenticatorAsync();
    }

    /// <summary>
    /// 文件来源：模板目录里的每个 JSON 算作版本 1 的已发布模板。目录不存在时以空缓存启动并记录警告。
    /// </summary>
    /// <param name="services">根服务。</param>
    /// <param name="directory">模板目录。</param>
    /// <param name="logger">日志。</param>
    private static void LoadTemplateFiles(IServiceProvider services, string directory, ILogger logger)
    {
        var cache = services.GetRequiredService<PublishedTemplateCache>();
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("模板目录 {Directory} 不存在，没有可用模板", directory);
            cache.Load([]);
            return;
        }

        var templates = SeedFiles.ReadTemplates(directory).Select(template => (template.Code, new PublishedTemplate(template, 1))).ToList();
        cache.Load(templates);
        logger.LogInformation("已加载 {Count} 个模板：{Codes}", templates.Count, string.Join(", ", templates.Select(item => item.Code)));
    }

    /// <summary>
    /// 文件来源：版式目录里的 JSON 全部当作启用的版式，检查不通过的跳过并记录警告。
    /// </summary>
    /// <param name="services">根服务。</param>
    /// <param name="directory">版式目录。</param>
    /// <param name="logger">日志。</param>
    private static void LoadLayoutFiles(IServiceProvider services, string directory, ILogger logger)
    {
        var layouts = new List<SheetLayout>();
        if (Directory.Exists(directory))
        {
            foreach (var layout in SeedFiles.ReadLayouts(directory).Select(SheetLayoutRules.Normalize))
            {
                var problems = SheetLayoutRules.Validate(layout);
                if (problems.Count > 0)
                {
                    logger.LogWarning("版式文件 {Code} 检查不通过，跳过：{Problems}", layout.Code, string.Join("；", problems));
                    continue;
                }

                layouts.Add(layout);
            }
        }

        services.GetRequiredService<SheetLayoutCache>().Load(layouts);
        logger.LogInformation("拼版版式：从 {Directory} 加载 {Count} 个", directory, layouts.Count);
    }
}
