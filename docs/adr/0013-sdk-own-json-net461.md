# 0013 SDK 自带 JSON 读写，最低支持 .NET Framework 4.6.1

状态：已采纳（2026-10-12 用户决定：“4.6.1 就按第二种，SDK 正式支持吧”）
日期：2026-10-12
关联：T-042、ADR 0002（SDK 独立于后端）、AGENTS.md R4

## 背景

SDK 原来最低支持 .NET Framework 4.6.2：HTTP 用框架自带的 HttpClient（4.5 起就有），JSON 用微软的 System.Text.Json 8.x 包。System.Text.Json 8.x 官方只支持 4.6.2 起，4.6.1 的项目引用 SDK 能编译、能跑，但有 3 个“不支持 net461、没测过”的警告；只装了 4.6.1 的机器上还可能遇到 netstandard 垫片 DLL 的版本冲突。用户的客户里有 4.6.1 的老系统，要求正式支持。

## 决定

- SDK 去掉对 System.Text.Json 的依赖，改用自带的小 JSON 读写（`Internal/JsonText.cs`，只覆盖开放接口用到的部分：写请求体，读错误响应、字段定义、模板列表）。所有目标框架用同一套代码。
- 目标框架改成 `net461`、`netstandard2.0`、`net8.0`（net461 的 DLL 4.6.2–4.8.1 也用它）。SDK 不依赖任何 NuGet 包，net461 只用框架自带的 System.Net.Http。
- 公开 API 里原来是 `JsonElement` 的两处改成普通的 .NET 值：`TemplateSchema.Sample` 是 `Dictionary<string, object?>`，`TemplateField.Default` 是 `object?`；值是 string、decimal、bool（JSON 对象、数组读成 Dictionary、List）。样例值可以直接 `request.Set(key, value)`。这是不兼容改动，还在预览期，记在 1.0.0-preview.3 的变更记录里。
- AGENTS.md R4 改成“必须能编译到 net461，不依赖任何 NuGet 包”。

## 备选方案

- **维持 4.6.2，4.6.1 的项目加 `SuppressTfmSupportBuildWarnings`**：改动最小，但不是官方支持，只装了 4.6.1 的机器上有 DLL 冲突的风险。
- **改用 System.Text.Json 6.x（还带 net461 版本）**：6.x 已经停止支持、不再有安全修复。
- **用 Newtonsoft.Json**：R4 不允许（调用方的老系统里常有各种版本，最容易冲突）。
- **同时支持 4.0、4.5**：4.0 没有 HttpClient 和 async/await，要另写一套同步实现；用户只要求到 4.6.1。以后要支持 4.5，JSON 已经不是障碍，加一个 net45 目标即可。

## 影响

- SDK 少了 System.Text.Json、System.Memory 等 7 个 DLL，老系统里不会再因为这些 DLL 的版本冲突。
- JSON 读写由我们自己维护：调用端检查程序覆盖转义、中文、数字、嵌套、格式错误、属性名大小写和响应映射，端到端检查照旧跑真实服务。
- 演示程序 .NET Framework 版的内置模拟服务还用 System.Text.Json（演示程序自己引用这个包），不影响 SDK。
