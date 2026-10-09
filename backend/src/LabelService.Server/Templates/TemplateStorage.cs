using LabelService.Server.Data;
using LabelService.Server.Layouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Templates;

/// <summary>
/// 模板存储（连同拼版版式）的注册和启动初始化。来源由 LabelService:Templates:Source 决定，默认数据库。
/// </summary>
internal static class TemplateStorage
{
    /// <summary>
    /// 示例模板所在的业务分组编码。
    /// </summary>
    public const string SampleBusinessCode = "SAMPLE";

    /// <summary>
    /// 注册模板存储。配置在第一次用到时才读取，检查程序覆盖的配置（如临时数据目录）能生效。
    /// </summary>
    public static IServiceCollection AddTemplateStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TemplateStoreOptions>(configuration.GetSection(TemplateStoreOptions.SectionName));
        services.Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SqlitePragmaInterceptor>();
        services.AddDbContextFactory<LabelDbContext>((provider, options) => options
            .UseSqlite(provider.GetRequiredService<IOptions<DataOptions>>().Value.ConnectionString())
            .AddInterceptors(provider.GetRequiredService<SqlitePragmaInterceptor>()));
        services.AddSingleton<DbTemplateStore>();
        services.AddSingleton<FileTemplateStore>();
        services.AddSingleton<TemplatePublishValidator>();
        services.AddSingleton<TemplateRepository>();
        services.AddLayoutStorage();
        services.AddSingleton<ITemplateStore>(provider =>
            provider.GetRequiredService<IOptions<TemplateStoreOptions>>().Value.Source == TemplateSource.Files
                ? provider.GetRequiredService<FileTemplateStore>()
                : provider.GetRequiredService<DbTemplateStore>());
        return services;
    }

    /// <summary>
    /// 启动时调用一次，在开始接收请求之前：建数据目录、执行迁移、开发环境导入示例模板、把已发布版本加载进内存。
    /// </summary>
    public static async Task InitializeTemplateStorageAsync(this WebApplication app)
    {
        var services = app.Services;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TemplateStorage));
        var options = services.GetRequiredService<IOptions<TemplateStoreOptions>>().Value;
        if (options.Source == TemplateSource.Files)
        {
            services.GetRequiredService<ITemplateStore>();
            LayoutStorage.LoadFiles(services, options.ResolveSeedLayoutDirectory(), logger);
            return;
        }

        var directory = services.GetRequiredService<IOptions<DataOptions>>().Value.ResolveDirectory();
        Directory.CreateDirectory(directory);
        var contexts = services.GetRequiredService<IDbContextFactory<LabelDbContext>>();
        bool empty;
        await using (var db = await contexts.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
            empty = !await db.Templates.AnyAsync();
        }

        var repository = services.GetRequiredService<TemplateRepository>();
        if (empty && app.Environment.IsDevelopment())
        {
            await ImportSamplesAsync(repository, contexts, options.ResolveSeedDirectory(), logger);
        }

        var count = await repository.LoadPublishedAsync();
        logger.LogInformation("模板数据库 {Directory}：已加载 {Count} 个已发布版本", directory, count);
        await LayoutStorage.InitializeDatabaseAsync(services, app.Environment.IsDevelopment(), options.ResolveSeedLayoutDirectory(), logger);
    }

    /// <summary>
    /// 开发环境首次建库：把示例模板导入为已发布版本，放在业务分组“示例”（SAMPLE）里，走正常的保存草稿和发布流程。
    /// </summary>
    private static async Task ImportSamplesAsync(TemplateRepository repository, IDbContextFactory<LabelDbContext> contexts, string directory, ILogger logger)
    {
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("示例模板目录 {Directory} 不存在，跳过导入", directory);
            return;
        }

        int businessId;
        await using (var db = await contexts.CreateDbContextAsync())
        {
            var business = await db.Businesses.SingleOrDefaultAsync(item => item.Code == SampleBusinessCode);
            if (business is null)
            {
                business = new BusinessRecord { Code = SampleBusinessCode, Name = "示例", Description = "开发环境自动导入的示例模板", CreatedAt = DateTime.UtcNow };
                db.Businesses.Add(business);
                await db.SaveChangesAsync();
            }

            businessId = business.Id;
        }

        foreach (var template in FileTemplateStore.ReadDirectory(directory))
        {
            try
            {
                await repository.SaveDraftAsync(template, "system", businessId);
                await repository.PublishAsync(template.Code, "system");
                logger.LogInformation("已导入示例模板 {Code}", template.Code);
            }
            catch (ManagementException ex)
            {
                logger.LogWarning("示例模板 {Code} 没有发布：{Message} {Details}", template.Code, ex.Message, string.Join("；", ex.Details));
            }
        }
    }
}
