# 标签生成服务（LabelService）

一个独立的标签生成服务：在网页设计器里拖拽画出标签模板（文本、一维码、二维码、图片、图标、线、矩形），定义数据字段；业务系统（MES、WMS、ERP）传“模板编码 + JSON 数据”，服务返回可以直接打印的 PNG、PDF 或 ZPL 文件。另外提供给第三方用的 .NET SDK 和 WinForms 调用演示程序。

核心性能指标：单张标签服务端处理 P95 ≤ 100 ms。

## 三个部分

仓库分成前端、后端、调用端三个文件夹，各自独立编译、独立检查，互不引用项目，只通过 HTTP 和模板 JSON 交互（[ADR 0004](docs/adr/0004-three-parts.md)）。

| 文件夹 | 内容 | 技术 | 入口 |
| --- | --- | --- | --- |
| [frontend/](frontend/README.md) | 网页设计器、管理后台 | Vue 3 + TypeScript + Vite + Konva | `package.json` |
| [backend/](backend/README.md) | 契约、渲染引擎、服务端 | .NET 10、ASP.NET Core、SkiaSharp、SQLite | `LabelService.Backend.slnx` |
| [client/](client/README.md) | 调用端：SDK、WinForms 演示程序 | .NET（SDK 兼容 .NET Framework 4.6.2） | `LabelService.Client.slnx` |

```
LabelService/
├─ frontend/        前端
├─ backend/         后端
├─ client/          调用端
├─ docs/            三个部分共用的文档：设计书、原型、契约、架构、性能、ADR、任务
├─ verify.ps1       一次验证三个部分
├─ AGENTS.md        给 AI 助手的项目说明（人也请读）
├─ README.md        本文件
└─ .editorconfig  .gitignore  .gitattributes  global.json   全仓库共用的配置
```

## 当前状态

骨架阶段（2026-10）。整条链路已经打通，各个功能按任务逐步填充，进度见[任务看板](docs/tasks/README.md)。

| 部分 | 状态 |
| --- | --- |
| 模板 JSON 规范、错误码、协议 | 已定稿（`backend/src/LabelService.Contracts`） |
| 单张生成接口 `POST /api/v1/render` | PNG 可用；元素只实现了线、矩形；PDF、ZPL 返回 501 |
| 字段定义接口 `GET /api/v1/templates/{code}/schema` | 可用 |
| 批量生成、服务端预览 | 未开始，返回 501 |
| 模板数据库、版本发布、管理后台 | 未开始（数据库用 SQLite，单实例部署） |
| 客户端 SDK | 公开 API 完整，对真实服务端到端检查通过 |
| WinForms 演示程序 | 最小可用版本 |
| 网页设计器 | 骨架：能显示示例模板、选中、拖动 |
| 监控、预热、部署 | 指标已埋点，未接入 Prometheus |

## 快速开始

需要 .NET SDK 10.0.100 以上、Node.js 20.19+ 或 22.12+；WinForms 演示程序需要 Windows。

一次验证全部三个部分（编译、格式检查、检查程序、前端打包，并临时启动后端跑 SDK 端到端检查）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1
```

启动后端服务（`http://localhost:5080`，开发环境自带测试 Key `lbl_test_local_dev`），另开一个终端生成一张箱标：

```powershell
dotnet run --project backend/src/LabelService.Server
curl.exe -o box.png -H "X-Api-Key: lbl_test_local_dev" -H "Content-Type: application/json" --data-binary "@backend/samples/requests/box-label.render.json" http://localhost:5080/api/v1/render
```

响应头 `Server-Timing` 会列出服务端每个阶段的耗时。前端和调用端的命令见各自的 README。

## 文档

| 想了解 | 看这里 |
| --- | --- |
| 为什么做、做什么、怎么做 | [设计说明书](docs/design/label-service-design.html)（用浏览器打开） |
| 界面长什么样 | [网页端原型](docs/prototypes/label-designer-prototype.html)、[演示程序原型](docs/prototypes/label-sdk-demo-prototype.html) |
| 代码怎么分层、请求怎么走 | [架构说明](docs/architecture.md) |
| 模板 JSON、接口、错误码 | [契约](docs/contracts.md) |
| 性能目标和怎么测 | [性能约定](docs/performance.md) |
| 为什么这样决定 | [架构决策记录](docs/adr/README.md) |
| 还有什么没做、谁在做 | [任务看板](docs/tasks/README.md) |

## 参与开发

这个仓库计划由多个 AI 助手和人一起开发。**不管是人还是 AI，动手前请先读 [AGENTS.md](AGENTS.md)**：里面有硬性规则、协作流程、完成定义和常用命令。简单说：

1. 每项工作对应 `docs/tasks/` 下的一个任务文件，先认领再动手，做完写交接记录。
2. 一般只改一个部分；改模板 JSON、接口、错误码属于契约变更，三个部分和文档要一起改。
3. 交付前运行 `verify.ps1`（或只验证改动的部分：`-Part backend`、`-Part client`、`-Part frontend`）。

代码风格沿用公司 DB Studio 的约定：中文 XML 注释、4 空格、CRLF、控制台检查程序；提交信息用英文 Conventional Commits。

## 许可证

本项目以 [Apache License 2.0](LICENSE) 发布。

## 第三方组件

SkiaSharp（MIT）、ZXing.Net（Apache-2.0）、Svg.Skia（MIT）、Vue（MIT）、Konva（MIT）、vue-konva（MIT）、bwip-js（MIT）、BenchmarkDotNet（MIT）。字体只使用思源黑体、思源宋体（SIL OFL 1.1）。
