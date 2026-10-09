# LabelService.Client

标签生成服务的 .NET 客户端：几行代码生成标签文件（PDF、PNG、ZPL），自带超时、重试和错误码映射，还有把 ZPL 直接送到标签打印机的辅助类。

- 目标框架：`net462`（.NET Framework 4.6.2–4.8.1）、`netstandard2.0`、`net8.0`（.NET 8、9、10）
- 依赖：.NET Framework 和 netstandard2.0 只依赖 `System.Text.Json`；net8.0 无额外依赖。不依赖 Newtonsoft.Json，也不依赖任何日志框架。

## 快速开始

1. 找标签服务管理员要：服务地址、一把 API Key（测试环境的 Key 以 `lbl_test_` 开头）、要调用的模板编码。
2. 引用包：`dotnet add package LabelService.Client`，或者把 zip 里对应框架的 `LabelService.Client.dll`（和 `System.Text.Json`）加到项目引用。
3. 应用里只建一个客户端实例，多线程共享：

```csharp
using LabelService.Client;
using LabelService.Client.Printing;

var client = new LabelServiceClient(new LabelServiceClientOptions
{
    BaseAddress = new Uri("https://label.example.com/"),
    ApiKey = ConfigurationManager.AppSettings["LabelApiKey"],   // 放配置文件或环境变量，不要写进代码
});

// 单张：生成 PDF 并保存
var request = new RenderRequest("BOX_LABEL", LabelFormat.Pdf)
    .Set("partName", "电源适配器")
    .Set("qty", 50)
    .Set("prodDate", DateTime.Today)
    .Set("boxNo", "C2610080001");
LabelFile file = await client.RenderAsync(request);
file.Save(Path.Combine(@"D:\Labels", file.FileName));

// 批量：公共字段只传一次，生成一个 ZPL 文件直接发给斑马打印机
var batch = new BatchRenderRequest("BOX_LABEL", LabelFormat.Zpl)
    .SetCommon("partName", "电源适配器")
    .SetCommon("batchNo", "20261008-A");
foreach (var boxNo in boxNumbers)
    batch.AddItem().Set("boxNo", boxNo);
LabelFile zpl = await client.RenderBatchAsync(batch);
await NetworkPrinter.SendAsync("192.168.1.50", 9100, zpl.Content);
```

要先知道模板要哪些字段：`TemplateSchema schema = await client.GetSchemaAsync("BOX_LABEL");`，里面有字段 key、类型、是否必填和调用样例。

## 错误处理

服务端错误和网络错误都转成 `LabelServiceException`：`ErrorCode`、`Message`（中文）、`StatusCode`、`TraceId`、`Errors`（逐条：第几条、哪个字段）、`IsTransient`（暂时性错误，SDK 已经重试过）。

```csharp
try
{
    LabelFile file = await client.RenderBatchAsync(batch);
}
catch (LabelServiceException ex) when (ex.ErrorCode == LabelErrorCodes.FieldRequired)
{
    foreach (var e in ex.Errors)
        log.Warn($"第 {e.Index + 1} 条缺少 {e.Field}");
}
catch (LabelServiceException ex) when (ex.IsTransient)
{
    // 网络或服务暂时不可用，稍后再试，或者转人工补打
}
```

| 情况 | ErrorCode | 自动重试 | 怎么处理 |
| --- | --- | --- | --- |
| 连不上、DNS 失败 | `NETWORK_ERROR` | 是 | 检查服务地址和网络 |
| 超过 Timeout | `TIMEOUT` | 是 | 批量太大时拆批，或调大 Timeout |
| 502、503、504 | `SERVICE_UNAVAILABLE` | 是 | 服务繁忙或在重启，稍后再试 |
| 无法识别的响应（如代理返回的 HTML 错误页） | `UNEXPECTED_RESPONSE` | 否 | 核对服务地址，检查中间的代理或网关 |
| 401 | `UNAUTHORIZED` | 否 | Key 填错、被停用或已重置，或者来源 IP 不在白名单，找管理员 |
| 403 | `TEMPLATE_FORBIDDEN` | 否 | 这把 Key 没有这个模板所属业务的权限，找管理员放开 |
| 404 | `TEMPLATE_NOT_FOUND` | 否 | 核对模板编码，确认模板已发布 |
| 400 | `FIELD_REQUIRED`、`FIELD_TYPE_INVALID` | 否 | 按 `ex.Errors` 修正数据 |
| 400 | `BARCODE_INVALID`、`BARCODE_TOO_DENSE`、`IMAGE_INVALID` | 否 | 条码内容不符合码制、太长放不下，或图片坏了 |
| 400 | `INVALID_JSON`、`FORMAT_INVALID`、`DPI_INVALID`、`OUTPUT_INVALID`、`COPIES_INVALID` | 否 | 请求参数不对（SDK 会先在本地拦一部分） |
| 413 | `BATCH_TOO_LARGE` | 否 | 拆成多批 |
| 500 | `RENDER_FAILED` | 否 | 带 traceId 联系标签服务管理员 |

## 选项

| 选项 | 默认值 | 说明 |
| --- | --- | --- |
| `BaseAddress` | — | 服务地址，必填 |
| `ApiKey` | — | 必填 |
| `Timeout` | 30 秒 | 单次请求超时 |
| `MaxRetries` | 2 | 网络错误、超时和 502、503、504 的重试次数，间隔 `RetryDelay × 第几次` |
| `RetryDelay` | 0.5 秒 | |
| `MaxLabelsPerRequest` | 500 | 批量总张数（含份数）上限，超过时本地直接抛 `ArgumentException` |
| `UserAgent` | `LabelService.Client/版本号` | |

## 日志和对账

SDK 不依赖日志框架。每次调用结束触发 `CallCompleted` 事件，给出操作、模板、状态码、错误码、总耗时、服务端耗时（来自 `Server-Timing`）、尝试次数、`RequestId` 和 `TraceId`，接到自己的日志里：

```csharp
client.CallCompleted += (sender, e) =>
    log.Info($"{e.Operation} {e.TemplateCode} {e.StatusCode} {e.ErrorCode} {e.Duration.TotalMilliseconds:0}ms 服务端 {e.ServerDuration?.TotalMilliseconds:0}ms 尝试 {e.Attempts} 次 requestId={e.RequestId} traceId={e.TraceId}");
```

每个请求自动带 `X-Request-Id`（GUID），服务端写进调用日志。反馈问题时带上 traceId 或 requestId。

## 常见问题

**WinForms 界面卡住？** 同步方法（`Render`、`RenderBatch`）不会死锁，但会卡住界面线程，界面代码用 `RenderAsync`。

**每次调用都 new 一个客户端可以吗？** 不要。客户端内部复用 HttpClient 和长连接，每次新建会耗尽端口、每次都要重新握手。应用里一个实例就够了，它是线程安全的。

**日期、图片字段怎么传？** `DateTime` 自动转成 ISO 8601（只有日期部分时转成 `yyyy-MM-dd`）；图片字段传 `byte[]`，自动转 base64（不超过 1 MB，不支持 URL）；值为 null 的字段不发送。

**USB 连接的标签打印机？** 用 `Printing.WindowsRawPrinter.Send(打印机名称, zpl.Content)` 以 RAW 方式写入 Windows 打印队列。网络打印机用 `NetworkPrinter.SendAsync(地址, 9100, 内容)`。PDF 交给系统默认程序打印。

**PNG 批量为什么报错？** PNG 一个文件只能放一张，批量要选 `BatchOutput.Zip`。

**TLS 1.2？** .NET Framework 4.6.2 上如果服务是 HTTPS，确认系统启用了 TLS 1.2（4.7 以上默认跟随系统设置）。

## 版本

语义化版本：1.x 只做向后兼容的改动；接口升到 `/api/v2` 时 SDK 升 2.0，v1 接口至少保留 12 个月。预览版带 `-preview.N`。变更见 CHANGELOG.md。
