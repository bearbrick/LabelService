using LabelService.Rendering.Fonts;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Fonts;

/// <summary>
/// 字体目录的注册和加载。字体在启动时加载一次，渲染期间只读（R3）。
/// </summary>
internal static class FontSetup
{
    /// <summary>
    /// 注册字体注册表。<see cref="LoadFonts"/> 在开始接收请求前触发加载。
    /// </summary>
    public static IServiceCollection AddLabelFonts(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FontOptions>(configuration.GetSection(FontOptions.SectionName));
        services.AddSingleton(provider =>
        {
            var directory = provider.GetRequiredService<IOptions<FontOptions>>().Value.ResolveDirectory();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(FontSetup));
            var fonts = FontRegistry.Load(directory);
            foreach (var error in fonts.Errors)
            {
                logger.LogWarning("{Error}", error);
            }

            if (fonts.HasDefaultFamily)
            {
                logger.LogInformation("已加载字体 {Families}（{Count} 个文件，目录 {Directory}）", string.Join("、", fonts.FamilyNames), fonts.Files.Count, directory);
            }
            else
            {
                logger.LogWarning("字体目录 {Directory} 里没有思源黑体，文本会降级显示，中文可能是方框。准备字体见 backend/assets/fonts/README.md", directory);
            }

            return fonts;
        });
        return services;
    }

    /// <summary>
    /// 启动时加载字体，避免第一次请求时才加载。
    /// </summary>
    public static void LoadFonts(this WebApplication app) => app.Services.GetRequiredService<FontRegistry>();
}

/// <summary>
/// 配置节 LabelService:Fonts。
/// </summary>
internal sealed class FontOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Fonts";

    /// <summary>
    /// 字体目录，相对路径相对于程序目录。默认程序目录下的 fonts/（构建时从 backend/assets/fonts 复制）。
    /// </summary>
    public string Directory { get; set; } = "fonts";

    /// <summary>
    /// 字体目录的绝对路径。
    /// </summary>
    public string ResolveDirectory() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, Directory));
}
