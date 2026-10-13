# 变更记录

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循[语义化版本](https://semver.org/lang/zh-CN/)：1.x 只做向后兼容的改动，预览版带 `-preview.N`。

## [1.0.0-preview.5] - 2026-10-13

### 新增

- `TemplateField.Source`：字段定义接口的可选来源（input/template/null）；`RequiresInput` 区分是否需由接口填写。旧响应省略来源时保持原行为。
- 演示程序模板提供字段不要求填写、输入只读，生成请求和复制代码排除这些字段。

## [1.0.0-preview.4] - 2026-10-13

### 新增

- `RenderBatchFilesAsync` / `RenderBatchFiles` 扩展方法、`LabelBatchResult`、`LabelBatchItem`、`LabelBatchUnit`：一次生成后自动得到业务条目或拼版纸页集合，保留原始文件、DPI、实际模板版本及生成份数；兼容原客户端接口实现者。
- 可选 Windows 打印扩展提供集合、单项、子集的 `Print` / `CreateDocument` 重载，PNG/PDF 连续分页、ZPL 单个 RAW 作业；PDF 拼版按原文档页引用打印，不重新生成。
- 演示程序保留生成集合，支持“静默打印所选项”，生成代码包含整批、单项和子集用法。

### 修复

- Windows 系统 PDF 引擎在桌面缩放下的位图像素数与请求 DPI 不一致：归一位图尺寸，保留 PDF 实际纸张尺寸。

原 `RenderBatchAsync` 和 `Print(LabelFile)` 保持可用，SDK 仍无 NuGet 依赖。

## [1.0.0-preview.3] - 2026-10-12

### 新增

- `ListTemplatesAsync` / `ListTemplates`：列出这把 API Key 有权使用的已发布模板（`TemplateSummary`：编码、名称、最新版本、尺寸），对应服务端新增的 `GET /api/v1/templates`。`CallCompleted` 的操作名是 `ListTemplates`。
- `TemplateField.Auto`：字段的自动值（如 `today`、`today+30d`、`index`、`client`），有自动值的字段不传时服务端自动算（服务端 T-038）。

- 支持 .NET Framework 4.6.1：目标框架改成 `net461`、`netstandard2.0`、`net8.0`（ADR 0013）。

### 变更

- `ILabelServiceClient` 增加上面两个方法：自己实现了这个接口的调用方（单元测试里的替身）要补上。
- 不再依赖 System.Text.Json（JSON 读写是 SDK 自带的），SDK 没有任何 NuGet 依赖。
- **不兼容：** `TemplateSchema.Sample` 从 `Dictionary<string, JsonElement>` 改成 `Dictionary<string, object?>`，`TemplateField.Default` 从 `JsonElement?` 改成 `object?`；值是 string、decimal、bool（JSON 对象、数组是 Dictionary、List）。原来写 `sample.GetString()`、`GetRawText()` 的改成 `Convert.ToString(value, CultureInfo.InvariantCulture)`；样例值可以直接 `request.Set(key, value)`。
- `Set(key, value)` 传 `JsonElement` 时按文本发送（原来按 JSON 原样发送）；数字、布尔请传 .NET 的数字、bool。

## [1.0.0-preview.2] - 2026-10-10

### 新增

- 拼版（服务端 T-018）：`RenderRequest`、`BatchRenderRequest` 增加 `Layout`（版式编码）和 `StartSlot`（第一页从第几个格子开始）；`LabelFile.PageCount` 是页数（`X-Page-Count`，不用版式时等于 `LabelCount`）。
- 错误码 `LayoutNotFound`、`LayoutSizeMismatch`、`StartSlotInvalid`。

### 变更

- 用了版式时，PNG 单张可以多份、PNG 批量不要求 `BatchOutput.Zip`；ZPL 加版式、`StartSlot` 小于 1 时本地抛 `ArgumentException`。

## [1.0.0-preview.1] - 2026-10-10

第一个预览版。

### 新增

- `LabelServiceClient`：单张生成 `RenderAsync` / `Render`、批量生成 `RenderBatchAsync` / `RenderBatch`、字段定义 `GetSchemaAsync` / `GetSchema`，实现 `ILabelServiceClient`，可以在单元测试里替换。
- 超时（默认 30 秒）和重试：网络错误、超时和 502、503、504 自动重试 2 次，间隔 0.5 秒、1 秒；其余错误不重试。服务端排队超时返回的 503（`SERVICE_UNAVAILABLE`）同样重试。
- 本地预校验：模板编码为空、份数不在 1–100、批量总张数超过 `MaxLabelsPerRequest`、PNG 批量没用 Zip 时直接抛 `ArgumentException`，不发请求。
- 值转换：`DateTime` 转 ISO 8601，`byte[]` 转 base64，枚举转字符串，null 不发送。
- `LabelFile`：内容、文件名、模板版本、张数、DPI、traceId、requestId、服务端耗时和分阶段耗时、警告；`Save`、`SaveAsync`、`OpenRead`、`GetZplText`。
- `LabelServiceException`：错误码、中文说明、HTTP 状态码、traceId、逐条错误、是否暂时性错误；`LabelErrorCodes` 和服务端错误码一一对应。
- `CallCompleted` 事件：每次调用的操作、模板、状态码、错误码、总耗时、服务端耗时、尝试次数、requestId、traceId。
- 每个请求自动带 `X-Api-Key`、`User-Agent`、`X-Request-Id`。
- `Printing.NetworkPrinter`（TCP 9100 发 ZPL）、`Printing.WindowsRawPrinter`（RAW 写入 Windows 打印队列）。
- 目标框架 net462、netstandard2.0、net8.0。

