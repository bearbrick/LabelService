# 0004 仓库分成前端、后端、调用端三个独立部分

状态：已采纳
日期：2026-10-09
关联：T-000；部分取代 0001 中“一个解决方案包含全部项目”的决定

## 背景

骨架最初是一个 `LabelService.slnx` 包含全部 .NET 项目，前端放在 `web/designer`。2026-10-09 确认改为三个文件夹分开管理，外层是 `LabelService` 文件夹，不用一个解决方案包含所有项目。

分开以后，多个 AI 并行开发时，前端、后端、调用端的任务不会碰到同一个解决方案、同一份包版本文件，冲突更少；第三方只关心调用端，也更容易单独拿出去。

## 决定

- 仓库根目录下三个部分，各自独立编译、独立检查：
  - `frontend/`：网页设计器和管理后台，npm 项目。
  - `backend/`：`LabelService.Backend.slnx`，包含 Contracts、Rendering、Server、后端检查程序、基准。
  - `client/`：`LabelService.Client.slnx`，包含 SDK、WinForms 演示程序、调用端检查程序。
- 每个 .NET 部分有自己的 `Directory.Build.props`、`src/Directory.Build.props`、`Directory.Packages.props`。全仓库共用的只有 `.editorconfig`、`global.json`、`.gitignore`、`.gitattributes`、文档和 `verify.ps1`。
- 三个部分**互不引用项目和文件**，只通过 HTTP 和模板 JSON 交互。检查程序会拦跨部分的项目引用和 import。
- 契约的权威定义在后端 Contracts。调用端（错误码、HTTP 头）和前端（模板模型、示例模板）各有一份副本。**跨部分的契约一致性检查放在后端检查程序里**，按文本读取另外两个部分的源码比对，不引用项目。
- SDK 对真实服务的端到端检查放在调用端检查程序里，只在设置 `LABEL_SERVICE_URL` 时运行。根目录 `verify.ps1` 全量运行时会临时启动后端服务并设置它。
- 文档、任务、ADR 仍在根目录 `docs/`，三个部分共用。

## 备选方案

- 保留一个解决方案、只调整文件夹：改动小，但不满足“分开管理”，所有 .NET 项目仍共用包版本文件和解决方案文件。
- 拆成三个仓库：边界最清楚，但契约改动要跨仓库同步，任务和文档也要分散，目前团队规模用不上。
- 后端 Contracts 打成 NuGet 包给调用端引用：和 ADR 0002（SDK 不引用契约程序集）冲突。

## 影响

- 契约变更必须同时改三处副本，并运行完整的 `verify.ps1`（AGENTS.md §6.4）。
- 只改一个部分时，可以只验证那个部分：`verify.ps1 -Part backend|client|frontend`。
- 前端的示例模板是后端的副本，管理后台（T-014）完成后改为从接口读取，副本可以删除。
- 0001 中“一个仓库、一个解决方案”的描述由本篇取代；技术栈的部分仍然有效。
