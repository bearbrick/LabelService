# LabelService.Client

标签生成服务的 .NET 客户端：几行代码生成标签文件（PDF、PNG、ZPL），自带超时、重试和错误码映射，还有把 ZPL 直接送到标签打印机的辅助类。

- 目标框架：`net461`（.NET Framework 4.6.1–4.8.1）、`netstandard2.0`、`net8.0`（.NET 8、9、10）
- 依赖：没有任何 NuGet 依赖（JSON 读写是自带的），.NET Framework 版使用框架自带的 System.Net.Http、System.IO.Compression。

## 快速开始

1. 找标签服务管理员要：服务地址、一把有效 API Key、已发布模板的编码和字段定义。Development 自带的 `lbl_test_local_dev` 也是实际配置的开发 Key，不代表任意字符串都可调用。
2. 使用管理员交付的包或私有源：`dotnet add package LabelService.Client --version 1.0.0-preview.5`，或者引用 zip 里对应框架的 DLL；不假定包已发布到公共 NuGet。静默打印再引用可选的 `LabelService.Client.Windows`。
3. 应用里只建一个客户端实例，多线程共享：

```csharp
using LabelService.Client;
using LabelService.Client.Printing;

var client = new LabelServiceClient(new LabelServiceClientOptions
{
    BaseAddress = new Uri("https://label.example.com/"),
    ApiKey = Environment.GetEnvironmentVariable("LABEL_API_KEY"),   // 放配置文件或环境变量，不要写进代码
});

// 单张：生成 PDF 并保存
var request = new RenderRequest("BOX_LABEL", LabelFormat.Pdf)
    .Set("partName", "电源适配器")
    .Set("partNo", "3100-0123")
    .Set("qty", 50)
    .Set("batchNo", "20261008-A")
    .Set("prodDate", DateTime.Today)
    .Set("boxNo", "C2610080001");
LabelFile file = await client.RenderAsync(request);
file.Save(Path.Combine(@"D:\Labels", file.FileName));

// 批量：公共字段只传一次，生成一个 ZPL 文件直接发给斑马打印机
var batch = new BatchRenderRequest("BOX_LABEL", LabelFormat.Zpl)
    .SetCommon("partName", "电源适配器")
    .SetCommon("partNo", "3100-0123")
    .SetCommon("qty", 50)
    .SetCommon("batchNo", "20261008-A");
foreach (var boxNo in boxNumbers)
    batch.AddItem().Set("boxNo", boxNo);
LabelFile zpl = await client.RenderBatchAsync(batch);
await NetworkPrinter.SendAsync("192.168.1.50", 9100, zpl.Content);
```

先查模板字段：`TemplateSchema schema = await client.GetSchemaAsync("BOX_LABEL");`，包含 key、类型、来源、必填、系统预设和调用样例。接口字段由客户传入；模板提供字段由服务取值，不接受同名数据覆盖；旧模板省略来源时保持回退。`client.ListTemplatesAsync()` 列出这把 Key 能用的已发布模板。

以下代码是集成片段：`boxNumbers` 为业务系统的箱号集合，字段以管理员发布的模板为准。内置 BOX_LABEL 的必填字段已在示例中给全；自定义模板提供的日期等字段不需要接口重复传。

### 拼版：多张标签排在 A4 上打

激光打印机打 A4 不干胶纸时，设 `Layout`（版式编码，找管理员要；版式的格子尺寸要和模板尺寸一致）。只支持 PDF 和 PNG：

```csharp
// 单张：Copies 就是铺几个格子，8 格的版式 8 份是一页 8 张相同的标签
var sheet = new RenderRequest("BOX_LABEL", LabelFormat.Pdf) { Layout = "A4_100x60_2x4", Copies = 8 }
    .Set("partName", "电源适配器")
    .Set("partNo", "3100-0123").Set("qty", 50)
    .Set("batchNo", "20261008-A").Set("boxNo", "C2610080001");

// 批量：每条按顺序占格子，一页排满换下一页；StartSlot = 3 表示第一页从第 3 格开始（前两格已经用掉了）
var boxes = new BatchRenderRequest("BOX_LABEL", LabelFormat.Pdf) { Layout = "A4_100x60_2x4", StartSlot = 3 }
    .SetCommon("partName", "电源适配器")
    .SetCommon("partNo", "3100-0123").SetCommon("qty", 50)
    .SetCommon("batchNo", "20261008-A");
foreach (var boxNo in boxNumbers)
    boxes.AddItem().Set("boxNo", boxNo);
LabelFile pages = await client.RenderBatchAsync(boxes);
Console.WriteLine($"{pages.LabelCount} 张标签，{pages.PageCount} 页");
```

PDF 是一个文件、一页一张纸；PNG 一页一张图，多页时是 ZIP（`IsZip` 为 true）。用了版式时批量的 `Output` 不起作用，PNG 单张也可以多份。

### 生成集合，再选择哪些打印

```csharp
LabelBatchResult result = await client.RenderBatchFilesAsync(batch);
WindowsLabelPrinter.Print(result.Items);                      // 打印整批
// 下面两种是替代上面一行的补打方式，不要一起执行：
// WindowsLabelPrinter.Print(result.Items[1]);                 // 只打第 2 项
// WindowsLabelPrinter.Print(new[] { result.Items[0], result.Items[2] }); // 第 1、3 项
```

`RenderBatchFilesAsync` / `RenderBatchFiles` 是 `ILabelServiceClient` 的扩展方法：不增加原接口的抽象成员，不要求已有接口实现者修改。SDK 固定请求快照并使用 ZIP 输出，一次批量调用后在内存拆分，不修改原请求；拼版沿用服务的 PNG/PDF 输出规则。网络重试仍按原客户端设置执行，`CallCompleted` 仍为 `RenderBatch`。

- `OriginalFile`：原始响应，可保存整批留底，保留服务计时、警告和追踪信息。
- `Unit == Item`：普通标签按业务条目组织，`ItemNumber` 从 1 开始。PNG 多份放在同一项的 `Files` 中，PDF/ZPL 的份数已经包含在文件内；`Copies` 表示该条生成的份数。
- `Unit == Page`：拼版按纸页组织，`PageNumber` 从 1 开始。PNG 每项为一张纸图，PDF 每项通过 `PdfPageIndex` 引用原文档的对应页。**打印 PDF 纸页时传 `LabelBatchItem`，不要只传 `item.Files[0]`，后者是整个原 PDF。** 拼版页的 `Copies` 为 1，PNG 页的 `LabelCount` 为 0（未知），实际整批标签数看 `OriginalFile.LabelCount`。
- `Items`、`Files` 是只读列表，每个原文件保留生成 DPI、实际模板版本和原字节，补打不重新请求服务。`Content` 延续旧 API 的 `byte[]`，调用方不要修改内容。
- 空集合、错误格式、损坏 ZIP、数量不符会报错；ZIP 解压总大小上限 256 MiB。集合提交前验证图形文件和 PDF 页号；PNG/PDF 一个连续分页文档、ZPL 一个 RAW 作业，按选择顺序提交，可重复选择某项。

原来的 `RenderBatchAsync` / `RenderBatch` 返回 `LabelFile`，`Print(file)` 打印完整结果，继续可用。

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
| 404 | `LAYOUT_NOT_FOUND` | 否 | 版式编码不对或版式已停用，找管理员 |
| 400 | `LAYOUT_SIZE_MISMATCH`、`START_SLOT_INVALID` | 否 | 换一个格子尺寸和模板相同的版式；StartSlot 不能超过版式的格子数 |
| 413 | `BATCH_TOO_LARGE` | 否 | 拆成多批 |
| 500 | `RENDER_FAILED` | 否 | 带 traceId 联系标签服务管理员 |

## 选项

字段定义的 `TemplateField.Source` 为 `input` 时只使用接口数据，为 `template` 时由模板提供（同名接口值不能覆盖），省略时保持旧版回退行为；`RequiresInput` 为 true 时调用方需要填写。系统预设和手写值互斥，模板提供字段不出现在 schema 的调用样例中。

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

**USB 连接的打印机？** 实施人员先装好驱动，引用 `LabelService.Client.Windows` 后调用 `WindowsLabelPrinter.Print(file)` 或 `Print(result.Items)`，使用默认打印机静默提交。PNG、PDF 无需阅读器；ZPL 设备必须支持 ZPL。网络 ZPL 也可用 `NetworkPrinter.SendAsync`。

**PNG 批量为什么报错？** PNG 一个文件只能放一张，批量要选 `BatchOutput.Zip`。

**HTTPS？** 旧版 .NET Framework 4.6.1/4.6.2 集成需确认客户系统的 TLS 配置与服务匹配；SDK 不绕过证书校验。正式部署使用有效证书，不把开发环境 HTTP 地址复制成生产配置。

## 版本

语义化版本：1.x 只做向后兼容的改动；接口升到 `/api/v2` 时 SDK 升 2.0，v1 接口至少保留 12 个月。预览版带 `-preview.N`。变更见 CHANGELOG.md。

当前为 `1.0.0-preview.5`。预览包、源码验证通过和软件提交打印成功不等于客户现场验收或正式发版完成。
