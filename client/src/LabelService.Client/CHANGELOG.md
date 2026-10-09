# 变更记录

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循[语义化版本](https://semver.org/lang/zh-CN/)：1.x 只做向后兼容的改动，预览版带 `-preview.N`。

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

### 计划

- 拼版（`Layout`、`StartSlot`、`LabelFile.PageCount`）随服务端拼版功能（T-018）加入，向后兼容。
