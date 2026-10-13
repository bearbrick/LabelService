# 标签服务（LabelService）

在网页上设计标签和唛头、定义数据字段并发布模板。MES、WMS、ERP 等业务系统传入“模板编码 + JSON 数据”，服务返回 PNG、PDF 或 ZPL；.NET SDK 将结果整理成集合，支持 Windows 静默打印整批、单项或指定子集。

## 当前状态

截至 2026-10-13，项目进入**功能收敛、定版前验收**阶段，后续以使用体验、兼容性和缺陷修复为主。SDK 当前为 `1.0.0-preview.5`，正式版本和现场验收尚未宣布完成。待确认事项集中在 [定版核对清单](docs/release-readiness.md)，历史实现过程见 [任务索引](docs/tasks/README.md)。

| 能力 | 当前实现 |
| --- | --- |
| 网页设计器 | 文本、一维码、二维码、图片、包装标志、线、矩形；拖拽、对齐、撤销、模板导入导出、服务端预览 |
| 模板和数据 | 草稿自动保存、版本发布和回滚；字段来源区分接口传入与模板提供，系统预设和手写值互斥 |
| 生成接口 | 单张、批量 PNG / PDF / ZPL；ZIP、公共字段、DPI、份数；A4 拼版及起始格子 |
| 管理后台 | 业务分组、模板库、调用方与 API Key、调用日志、素材与字体、用户和角色权限、系统品牌信息 |
| .NET 集成 | HTTP SDK 支持 net461 / netstandard2.0 / net8.0；SDK 无 NuGet 依赖；超时、重试和统一错误码 |
| Windows 打印 | PNG / PDF / ZPL 共用静默入口；默认使用已配置的打印机，PDF 不依赖阅读器；集合和单项补打 |
| 后端运行 | .NET 10，SQLite / SQL Server / PostgreSQL，单实例；启动预热、就绪检查、并发排队、日志和监控 |

性能目标为单张服务端 P95 ≤ 100 ms，**目标服务器的最终结果需现场压测确认**。完整验证通过不等于性能门禁、真实打印机和生产部署全部验收通过，详见 [性能约定](docs/performance.md)。

## 快速开始

需要 .NET SDK 10.0.100+、Node.js 22.18+ 或 24；WinForms 演示程序和 Windows 打印扩展需要 Windows。运行开发检查前，按 [字体说明](backend/assets/fonts/README.md) 准备思源黑体；字体文件不在 Git 中，正式发布包也不附带字体。

在仓库根目录验证全部三个部分：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1
```

分别在三个终端启动服务、网页和演示程序：

```powershell
# 后端：http://localhost:5080
dotnet run --project backend/src/LabelService.Api

# 前端：http://localhost:5173
npm --prefix frontend ci
npm --prefix frontend run dev

# 客户端演示：Windows
dotnet run --project client/samples/LabelService.Demo.WinForms -f net10.0-windows
```

开发环境首次启动会创建 `admin` / `admin123`，首次登录必须改密码。开发 API Key 为 `lbl_test_local_dev`，**仅 Development 环境有效**；正式环境在“调用方与密钥”里创建自己的 Key。

先等待就绪，再生成示例箱标：

```powershell
curl.exe http://localhost:5080/health/ready
curl.exe -o box.png -H "X-Api-Key: lbl_test_local_dev" -H "Content-Type: application/json" --data-binary "@backend/samples/requests/box-label.render.json" http://localhost:5080/api/v1/render
```

请求体使用 UTF-8 文件，避免 Windows 命令行发送中文时发生编码问题。`Server-Timing` 提供各阶段计时；调用日志记录到写完响应正文的总耗时。

## 仓库与使用入口

前端、后端、调用端各自独立编译，只通过 HTTP 和模板 JSON 交互。

| 目录 | 用途 | 说明 |
| --- | --- | --- |
| [frontend](frontend/README.md) | 网页设计器和管理后台 | Vue 3、TypeScript、Vite、Konva |
| [backend](backend/README.md) | 契约、渲染、领域、应用、基础设施和 API | ASP.NET Core、SkiaSharp、EF Core |
| [client](client/README.md) | HTTP SDK、Windows 打印扩展、WinForms 演示 | 支持 .NET Framework 4.6.1 集成 |
| [docs](docs/README.md) | 当前文档入口、定版清单、架构、历史记录 | 阅读顺序和权威来源说明 |

| 要做什么 | 看这里 |
| --- | --- |
| 集成生成和打印 | [SDK 用法](client/src/LabelService.Client/README.md)、[Windows 静默打印](client/src/LabelService.Client.Windows/README.md) |
| 接口与模板字段 | [开放接口与模板契约](docs/contracts.md)、[管理接口](docs/admin-api.md) |
| 部署、升级、备份与字体 | [部署说明](backend/deploy/README.md)、[监控配置](backend/deploy/monitoring/README.md) |
| 验证、浏览器回归、打印自检 | [测试入口](docs/testing.md) |
| 定版还有哪些事 | [定版核对清单](docs/release-readiness.md) |
| 后续能力怎么排 | [二期候选](docs/roadmap.md)，这些不属于当前定版承诺 |

## 维护

人和 AI 修改项目都先读 [AGENTS.md](AGENTS.md)。每项改动有任务文件，交付前通过统一验证；契约变更同步三个部分。`CLAUDE.md`、`GEMINI.md`、Copilot 入口只指向同一套规则，避免重复维护。

保留自动化检查、ADR、设计书和有效历史证据；临时截图、SDK 包、测试日志和本地数据放在 Git 忽略的目录。历史报告只证明对应提交的检查结果，当前候选重新验证，不用旧截图替代新验收。

源码许可见 [LICENSE](LICENSE)。
