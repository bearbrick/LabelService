using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Server.Auth;
using LabelService.Server.Diagnostics;
using LabelService.Server.Endpoints;
using LabelService.Server.Templates;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = RenderLimits.MaxRequestBodyBytes);
builder.Services.ConfigureHttpJsonOptions(options => LabelJson.Apply(options.SerializerOptions));
builder.Services.Configure<ApiClientOptions>(builder.Configuration.GetSection(ApiClientOptions.SectionName));
builder.Services.Configure<TemplateStoreOptions>(builder.Configuration.GetSection(TemplateStoreOptions.SectionName));
builder.Services.AddSingleton<ApiKeyAuthenticator>();
builder.Services.AddSingleton<ITemplateStore, FileTemplateStore>();
builder.Services.AddSingleton<ILabelRenderer, LabelRenderer>();
builder.Services.AddSingleton<LabelMetrics>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// 未处理的异常统一返回 RENDER_FAILED，异常本身由中间件写日志。
app.UseExceptionHandler(errorApp => errorApp.Run(async http =>
{
    var result = ApiErrors.Create(http, ErrorCodes.RenderFailed, "渲染内部错误，请凭 traceId 联系标签服务管理员");
    await result.ExecuteAsync(http);
}));

app.MapHealthChecks("/health");
app.MapPublicApi();

app.Run();

/// <summary>
/// 程序入口。声明为 public partial，供检查程序用 WebApplicationFactory 在进程内启动服务。
/// </summary>
public partial class Program;
