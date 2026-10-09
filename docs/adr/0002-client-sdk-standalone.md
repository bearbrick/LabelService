# 0002 客户端 SDK 不引用契约程序集

状态：已采纳
日期：2026-10-09
关联：T-000、T-011

## 背景

SDK 要发给第三方，目标框架包括 .NET Framework 4.6.2。Contracts 项目面向 .NET 10，使用了 System.Text.Json 的新特性（多态序列化、`JsonStringEnumMemberName` 等）。

## 决定

- `LabelService.Client` 不引用仓库里的任何项目，错误码（`LabelErrorCodes`）和 HTTP 头（`Internal/HeaderNames`）是 Contracts 的副本。
- SDK 只暴露调用方需要的模型（`RenderRequest`、`LabelFile`、`TemplateSchema` 等），不暴露模板元素模型。
- 检查程序核对副本一致：Contracts 的每个错误码都在 SDK 里有，SDK 独有的只有 4 个客户端错误码；HTTP 头完全一致。
- SDK 源码不使用 record、init、required 等需要新运行时类型的语法，检查程序也会核对。

## 备选方案

- Contracts 多目标编译到 netstandard2.0：要放弃或降级 .NET 10 的 JSON 特性，服务端代码会变得别扭。
- 用源码链接（`<Compile Include="..\Contracts\..." Link=...>`）共享常量：能编译，但 SDK 的公开 API 会跟着服务端内部结构变化，难以保持语义化版本。

## 影响

- 改错误码或 HTTP 头时必须同时改两处，否则检查程序失败。
- SDK 的版本节奏独立于服务端，按语义化版本发布（T-011）。
