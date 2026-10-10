using LabelService.Api;
using LabelService.Api.Diagnostics;
using LabelService.Api.Endpoints;
using LabelService.Api.Endpoints.Admin;
using LabelService.Api.Endpoints.OpenApi;
using LabelService.Api.Hosting;
using LabelService.Application;
using LabelService.Contracts.Protocol;
using LabelService.Infrastructure;
using LabelService.Infrastructure.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 作为 Windows 服务运行时接入服务控制管理器；控制台、Docker 里运行时不起作用（ADR 0008）。
builder.Host.UseWindowsService(options => options.ServiceName = "LabelService");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = RenderLimits.MaxRequestBodyBytes);

// 三层各一个注册入口（ADR 0010）。
builder.Services.AddLabelApplication(builder.Configuration);
builder.Services.AddLabelInfrastructure(builder.Configuration);
builder.Services.AddLabelApi();

var app = builder.Build();

// 开始接收请求之前：迁移数据库、初始化账号、导入示例、把模板、版式、调用方、字体、素材加载进内存。
await StartupInitializer.InitializeAsync(app.Services, app.Environment);

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

app.Run();

/// <summary>
/// 程序入口。声明为 public partial，供检查程序用 WebApplicationFactory 在进程内启动服务。
/// </summary>
public partial class Program;
