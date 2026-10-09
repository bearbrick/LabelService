using LabelService.Contracts;
using LabelService.Contracts.Protocol;
using LabelService.Rendering;
using LabelService.Server.Admin;
using LabelService.Server.Assets;
using LabelService.Server.Auth;
using LabelService.Server.Diagnostics;
using LabelService.Server.Endpoints;
using LabelService.Server.Fonts;
using LabelService.Server.Hosting;
using LabelService.Server.Logging;
using LabelService.Server.Templates;

var builder = WebApplication.CreateBuilder(args);

// 作为 Windows 服务运行时接入服务控制管理器；控制台、Docker 里运行时不起作用（ADR 0008）。
builder.Host.UseWindowsService(options => options.ServiceName = "LabelService");

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = RenderLimits.MaxRequestBodyBytes);
builder.Services.ConfigureHttpJsonOptions(options => LabelJson.Apply(options.SerializerOptions));
builder.Services.Configure<ApiClientOptions>(builder.Configuration.GetSection(ApiClientOptions.SectionName));
builder.Services.AddSingleton<ApiKeyAuthenticator>();
builder.Services.AddTemplateStorage(builder.Configuration);
builder.Services.AddSingleton<ILabelRenderer, LabelRenderer>();
builder.Services.AddSingleton<LabelMetrics>();
builder.Services.AddMetricsExport();
builder.Services.AddHealthChecks();
builder.Services.AddLabelFonts(builder.Configuration);
builder.Services.AddLabelAssets(builder.Configuration);
builder.Services.AddApiClients();
builder.Services.AddRenderLog(builder.Configuration);
builder.Services.AddAdmin(builder.Configuration);
builder.Services.AddLabelHosting(builder.Configuration);

var app = builder.Build();
await app.InitializeTemplateStorageAsync();
await app.InitializeApiClientsAsync();
await app.InitializeRenderLogAsync();
await app.InitializeAdminAsync();

// 未处理的异常统一返回 RENDER_FAILED，异常本身由中间件写日志。
app.UseExceptionHandler(errorApp => errorApp.Run(async http =>
{
    var result = ApiErrors.Create(http, ErrorCodes.RenderFailed, "渲染内部错误，请凭 traceId 联系标签服务管理员");
    await result.ExecuteAsync(http);
}));

app.MapLabelHealth();
app.MapMetrics();
app.MapPublicApi();
app.MapAdminApi();
app.MapFontFiles();
app.LoadFonts();
app.LoadAssets();

app.Run();

/// <summary>
/// 程序入口。声明为 public partial，供检查程序用 WebApplicationFactory 在进程内启动服务。
/// </summary>
public partial class Program;
