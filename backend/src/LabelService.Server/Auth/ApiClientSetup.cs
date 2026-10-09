using LabelService.Server.Templates;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Auth;

/// <summary>
/// 调用方的注册和启动加载。
/// </summary>
internal static class ApiClientSetup
{
    /// <summary>
    /// 注册调用方管理。鉴权器 <see cref="ApiKeyAuthenticator"/> 在 Program 里注册。
    /// </summary>
    public static IServiceCollection AddApiClients(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ApiClientRepository>();
        return services;
    }

    /// <summary>
    /// 启动时把数据库里的调用方加载进鉴权器（主库的迁移由模板存储初始化执行）。模板来源是文件时只用配置里的调用方。
    /// </summary>
    public static async Task InitializeApiClientsAsync(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<TemplateStoreOptions>>().Value.Source == TemplateSource.Files)
        {
            return;
        }

        await app.Services.GetRequiredService<ApiClientRepository>().ReloadAsync();
    }
}
