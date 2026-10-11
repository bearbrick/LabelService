using LabelService.Application.Identity;
using LabelService.Application.Logs;
using LabelService.Application.Resources;
using LabelService.Application.SystemSettings;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.SystemSettings;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Hosting;
using LabelService.Infrastructure.Logging;
using LabelService.Infrastructure.Persistence;
using LabelService.Infrastructure.Repositories;
using LabelService.Infrastructure.Security;
using LabelService.Infrastructure.Storage;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Fonts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelService.Infrastructure;

/// <summary>
/// 基础设施层的依赖注入注册入口（ADR 0010）。
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// 注册基础设施层：数据库、仓储和工作单元、密码哈希和登录令牌、操作日志、调用日志的后台写入、每日备份、字体和素材。
    /// </summary>
    /// <remarks>
    /// 主库的数据库上下文每个请求一个，仓储和工作单元共用它（一次操作里的修改一起保存）。
    /// 日志库的仓储每次操作新建一个上下文，因为调用日志由后台服务写入，不在请求里。
    /// </remarks>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置，读取各配置节。</param>
    /// <returns>同一个服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLabelInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));
        services.Configure<AssetOptions>(configuration.GetSection(AssetOptions.SectionName));
        services.Configure<FontOptions>(configuration.GetSection(FontOptions.SectionName));
        services.AddLabelPersistence(configuration);
        AddRepositories(services);

        // 账号安全和操作日志。
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<JwtAdminTokenService>();
        services.AddSingleton<IAdminTokenService>(provider => provider.GetRequiredService<JwtAdminTokenService>());
        services.AddScoped<IOperationLogger, OperationLogger>();

        // 后台任务：调用日志批量写入和定期清理、每日备份。
        services.AddSingleton<RenderLogWriterService>();
        services.AddHostedService(provider => provider.GetRequiredService<RenderLogWriterService>());
        services.AddSingleton<BackupService>();
        services.AddHostedService(provider => provider.GetRequiredService<BackupService>());

        // 字体、素材：启动时加载一次，渲染期间只读（R3）。
        services.AddSingleton(LoadFonts);
        services.AddSingleton(LoadAssets);
        services.AddSingleton<IAssetFileStore, AssetFileStore>();
        services.AddSingleton<IBrandingFileStore, BrandingFileStore>();
        services.AddSingleton<IFontFileStore, FontFileStore>();
        return services;
    }

    /// <summary>
    /// 注册主库的数据库上下文（每个请求一个）、仓储、工作单元，以及日志库的仓储。
    /// </summary>
    /// <param name="services">服务集合。</param>
    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<LabelDbContext>>().CreateDbContext());
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IApiClientRepository, ApiClientRepository>();
        services.AddScoped<ISheetLayoutRepository, SheetLayoutRepository>();
        services.AddScoped<ICanvasPresetRepository, CanvasPresetRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddSingleton<IRenderLogRepository, RenderLogRepository>();
        services.AddSingleton<IOperationLogRepository, OperationLogRepository>();
    }

    /// <summary>
    /// 加载部署目录（程序目录 fonts/）和上传目录（数据目录 fonts/，ADR 0011）里的字体。没有思源黑体时记警告：文本会降级显示，中文可能是方框。
    /// </summary>
    /// <param name="provider">根服务。</param>
    /// <returns>字体注册表。</returns>
    private static FontRegistry LoadFonts(IServiceProvider provider)
    {
        var directory = provider.GetRequiredService<IOptions<FontOptions>>().Value.ResolveDirectory();
        var uploadDirectory = FontFileStore.ResolveDirectory(provider.GetRequiredService<IOptions<DataOptions>>().Value);
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(FontRegistry));
        var fonts = FontRegistry.Load(directory, uploadDirectory);
        foreach (var error in fonts.Errors)
        {
            logger.LogWarning("{Error}", error);
        }

        if (fonts.HasDefaultFamily)
        {
            logger.LogInformation("已加载字体 {Families}（{Count} 个文件，部署目录 {Directory}，上传目录 {Upload}）", string.Join("、", fonts.FamilyNames), fonts.Files.Count, directory, uploadDirectory);
        }
        else
        {
            logger.LogWarning("没有思源黑体（部署目录 {Directory}，上传目录 {Upload}），文本会降级显示，中文可能是方框。在管理后台“素材与字体”页上传思源黑体（Noto Sans SC），见 backend/assets/fonts/README.md", directory, uploadDirectory);
        }

        return fonts;
    }

    /// <summary>
    /// 加载图标和图片素材：上传的（数据目录 assets/）优先于内置的（程序目录 images/）。
    /// </summary>
    /// <param name="provider">根服务。</param>
    /// <returns>素材库。</returns>
    private static AssetLibrary LoadAssets(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<AssetOptions>>().Value;
        var uploaded = Path.Combine(provider.GetRequiredService<IOptions<DataOptions>>().Value.ResolveDirectory(), AssetFileStore.UploadFolder);
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AssetLibrary));
        var assets = AssetLibrary.Load(options.ResolveIconDirectory(), [uploaded, options.ResolveImageDirectory()]);
        foreach (var error in assets.Errors)
        {
            logger.LogWarning("{Error}", error);
        }

        logger.LogInformation("已加载 {Icons} 个图标、{Images} 个图片素材", assets.IconNames.Count, assets.ImageIds.Count);
        return assets;
    }
}
