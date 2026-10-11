using LabelService.Application.ApiClients;
using LabelService.Application.ApiTest;
using LabelService.Application.Businesses;
using LabelService.Application.Dashboard;
using LabelService.Application.Diagnostics;
using LabelService.Application.Hosting;
using LabelService.Application.Identity;
using LabelService.Application.Layouts;
using LabelService.Application.Logs;
using LabelService.Application.OpenApi;
using LabelService.Application.Rendering;
using LabelService.Application.Resources;
using LabelService.Application.SystemSettings;
using LabelService.Application.Templates;
using LabelService.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LabelService.Application;

/// <summary>
/// 应用层的依赖注入注册入口（ADR 0010）。
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// 注册应用层：配置项、内存缓存、渲染引擎、应用服务和启动预热。
    /// </summary>
    /// <remarks>
    /// 生命周期的约定：内存缓存、渲染相关和开放接口的服务是单例（热路径上不创建对象图）；
    /// 管理后台的应用服务是每个请求一个（和仓储、工作单元共用一个数据库上下文）。
    /// </remarks>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置，读取各配置节。</param>
    /// <returns>同一个服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLabelApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<ApiClientOptions>(configuration.GetSection(ApiClientOptions.SectionName));
        services.Configure<TemplateStoreOptions>(configuration.GetSection(TemplateStoreOptions.SectionName));
        services.Configure<RenderOptions>(configuration.GetSection(RenderOptions.SectionName));
        services.Configure<RenderLogOptions>(configuration.GetSection(RenderLogOptions.SectionName));
        services.Configure<WarmupOptions>(configuration.GetSection(WarmupOptions.SectionName));

        // 开放接口的热路径：内存缓存、渲染引擎、并发限制、调用日志队列、指标。
        services.AddSingleton<PublishedTemplateCache>();
        services.AddSingleton<SheetLayoutCache>();
        services.AddSingleton<ApiKeyAuthenticator>();
        services.AddSingleton<ILabelRenderer, LabelRenderer>();
        services.AddSingleton<RenderGate>();
        services.AddSingleton<RenderLogQueue>();
        services.AddSingleton<LabelMetrics>();
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<ILabelRenderAppService, LabelRenderAppService>();

        // 启动预热：就绪检查读 WarmupState。
        services.AddSingleton<WarmupState>();
        services.AddHostedService<WarmupService>();

        // 管理后台共用的无状态组件。
        services.AddSingleton<TemplateChecker>();
        services.AddSingleton<TemplatePreviewRenderer>();
        services.AddSingleton<DraftLogThrottle>();
        services.AddSingleton<ITemplatePreviewAppService, TemplatePreviewAppService>();

        // 核心流程（启动导入示例时也用）和管理后台的应用服务：每个请求一个。
        services.AddScoped<TemplateManager>();
        services.AddScoped<SheetLayoutManager>();
        services.AddScoped<IAccountAppService, AccountAppService>();
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddScoped<IRoleAppService, RoleAppService>();
        services.AddScoped<IBusinessAppService, BusinessAppService>();
        services.AddScoped<IApiClientAppService, ApiClientAppService>();
        services.AddScoped<ITemplateAppService, TemplateAppService>();
        services.AddScoped<ISheetLayoutAppService, SheetLayoutAppService>();
        services.AddScoped<ICanvasPresetAppService, CanvasPresetAppService>();
        services.AddScoped<IAssetAppService, AssetAppService>();
        services.AddScoped<IFontAppService, FontAppService>();
        services.AddScoped<ILogAppService, LogAppService>();
        services.AddScoped<IDashboardAppService, DashboardAppService>();
        services.AddScoped<IApiTestAppService, ApiTestAppService>();
        services.AddScoped<ISystemInfoAppService, SystemInfoAppService>();
        return services;
    }
}
