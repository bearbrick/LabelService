using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using LabelService.Rendering.Assets;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Assets;

/// <summary>
/// 素材库（图标、图片素材）的注册和加载。启动时加载一次，渲染期间只读（R3）。
/// </summary>
internal static class AssetSetup
{
    /// <summary>
    /// 注册素材库。<see cref="LoadAssets"/> 在开始接收请求前触发加载。
    /// </summary>
    public static IServiceCollection AddLabelAssets(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AssetOptions>(configuration.GetSection(AssetOptions.SectionName));
        services.Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName));
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AssetOptions>>().Value;
            var uploaded = Path.Combine(provider.GetRequiredService<IOptions<DataOptions>>().Value.ResolveDirectory(), "assets");
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AssetSetup));
            var assets = AssetLibrary.Load(options.ResolveIconDirectory(), [uploaded, options.ResolveImageDirectory()]);
            foreach (var error in assets.Errors)
            {
                logger.LogWarning("{Error}", error);
            }

            logger.LogInformation("已加载 {Icons} 个图标、{Images} 个图片素材", assets.IconNames.Count, assets.ImageIds.Count);
            return assets;
        });
        return services;
    }

    /// <summary>
    /// 启动时加载素材，避免第一次请求时才加载。
    /// </summary>
    public static void LoadAssets(this WebApplication app) => app.Services.GetRequiredService<AssetLibrary>();
}

/// <summary>
/// 配置节 LabelService:Assets。上传的图片素材固定放在数据目录的 assets/ 下（ADR 0003），同名时优先于内置素材。
/// </summary>
internal sealed class AssetOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Assets";

    /// <summary>
    /// 图标目录，相对路径相对于程序目录。默认 icons/（构建时从 backend/assets/icons 复制）。
    /// </summary>
    public string IconDirectory { get; set; } = "icons";

    /// <summary>
    /// 内置图片素材目录，相对路径相对于程序目录。默认 images/（构建时从 backend/assets/images 复制）。
    /// </summary>
    public string ImageDirectory { get; set; } = "images";

    /// <summary>
    /// 图标目录的绝对路径。
    /// </summary>
    public string ResolveIconDirectory() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, IconDirectory));

    /// <summary>
    /// 内置图片素材目录的绝对路径。
    /// </summary>
    public string ResolveImageDirectory() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ImageDirectory));
}
