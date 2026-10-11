using LabelService.Api.Diagnostics;
using LabelService.Api.Hosting;
using LabelService.Api.Security;
using LabelService.Application.Identity;
using LabelService.Contracts;
using LabelService.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace LabelService.Api;

/// <summary>
/// 接口层的依赖注入注册入口（ADR 0010）。
/// </summary>
internal static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// 注册接口层：JSON 序列化选项、管理后台的登录校验和当前用户、指标导出、健康检查。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>同一个服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLabelApi(this IServiceCollection services)
    {
        // 接口的 JSON 和模板 JSON 用同一套选项：camelCase、不写 null。
        services.ConfigureHttpJsonOptions(options => LabelJson.Apply(options.SerializerOptions));

        // 管理后台：JWT 校验（令牌里的 sub 原样保留作为用户名），当前用户每个请求一个。
        services.AddHttpContextAccessor();
        services.AddScoped<HttpCurrentAdmin>();
        services.AddScoped<ICurrentAdmin>(provider => provider.GetRequiredService<HttpCurrentAdmin>());
        services.AddAuthentication().AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtAdminTokenService>((options, tokens) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = tokens.ValidationParameters;
        });

        services.AddSingleton<PrometheusExporter>();
        services.AddLabelHealthChecks();
        return services;
    }
}
