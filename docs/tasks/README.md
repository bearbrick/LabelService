# 任务看板

每个任务一个文件，**状态以任务文件为准**，这里只做索引，不写状态，避免多人同时改同一个文件。认领、交接流程见 AGENTS.md 的“多 AI 协作”。

查看全部任务状态：

```powershell
Select-String -Path docs/tasks/T-*.md -Pattern '^(状态|负责人)：'
```

状态取值：`待认领` → `进行中` → `待验收` → `已完成`；卡住时写 `阻塞`，并在交接记录里写原因。

## 索引

里程碑对应设计书第 12 节：M1 设计器原型、M2 渲染与接口、M3 字段与发布、M4 试点。

| 编号 | 标题 | 部分 | 里程碑 | 依赖 | 主要目录 |
| --- | --- | --- | --- | --- | --- |
| [T-000](T-000-scaffold.md) | 搭建仓库骨架 | 全部 | — | — | 全部 |
| [T-001](T-001-text-rendering.md) | 文本元素渲染与字体 | 后端 | M2 | — | Rendering/Elements、Rendering/Fonts、assets/fonts |
| [T-002](T-002-barcode-rendering.md) | 一维码渲染 | 后端 | M2 | T-001（可文字后补） | Rendering/Elements |
| [T-003](T-003-qrcode-rendering.md) | 二维码渲染 | 后端 | M2 | — | Rendering/Elements |
| [T-004](T-004-image-icon-rendering.md) | 图片和图标渲染 | 后端 | M2 | — | Rendering/Elements、assets/icons |
| [T-005](T-005-pdf-output.md) | PDF 输出 | 后端 | M2 | T-001 | Rendering/Output |
| [T-006](T-006-zpl-output.md) | ZPL 输出与黑白化 | 后端 | M2 | — | Rendering/Output |
| [T-007](T-007-batch-api.md) | 批量生成接口 | 后端 | M2 | T-005、T-006 | Server/Endpoints |
| [T-008](T-008-template-store.md) | 模板存储、版本发布与缓存 | 后端 | M3 | — | Server/Templates、Server/Data |
| [T-009](T-009-clients-and-logs.md) | 调用方管理与调用日志 | 后端 | M3 | T-008 | Server/Auth、Server/Logging |
| [T-010](T-010-monitoring.md) | 监控接入 | 后端 | M4 | — | Server/Diagnostics、deploy/monitoring |
| [T-011](T-011-sdk-release.md) | 客户端 SDK 发布 | 调用端 | M2 预览 / M4 1.0 | T-007 | src/LabelService.Client |
| [T-012](T-012-winforms-demo.md) | WinForms 演示程序完整版 | 调用端 | M2 | T-011 | samples/LabelService.Demo.WinForms |
| [T-013](T-013-designer-drawing.md) | 设计器绘制交互 | 前端 | M1 | — | src/ |
| [T-014](T-014-admin-api-ui.md) | 管理 API 与管理后台 | 后端 + 前端 | M3 | T-008 | backend：Server/Admin；frontend：src/ |
| [T-015](T-015-benchmarks-load-tests.md) | 基准门禁与压测 | 后端 | M4 | T-001～T-006 | tests/LabelService.Benchmarks、tests/load |
| [T-016](T-016-preview-and-fonts.md) | 服务端预览与字体下载 | 后端 + 前端 | M3 | T-001 | backend：Server/Endpoints；frontend：src/ |
| [T-017](T-017-warmup-concurrency-deploy.md) | 预热、并发控制与部署 | 后端 | M4 | T-008 | Server/Hosting、deploy |
| [T-018](T-018-sheet-layout.md) | A4 拼版：版式与拼版渲染 | 后端 + 调用端 | M3 | T-005（存库依赖 T-008） | Contracts、Rendering、Server；client：src/LabelService.Client |
| [T-019](T-019-sheet-designer.md) | 拼版设计器：拖拽单张标签到 A4 上排版 | 前端 | M3 | T-013、T-018 | src/ |
| [T-020](T-020-embedded-template.md) | 模板嵌套 | 后端 + 前端 | 二期 | T-008、T-013 | Contracts、Server；frontend：src/ |
| [T-021](T-021-phase1-acceptance-fixes.md) | 一期验收问题修复与总体 review | 全部 | 一期验收 | T-001～T-019 | 全部 |
| [T-022](T-022-adopt-adr-0007-0009.md) | 采纳 ADR 0007、0009：预览只在管理接口，监控自建 | 后端 + 文档 | 一期验收 | T-010、T-016、T-021 | Server/Endpoints、docs |
| [T-023](T-023-layered-architecture.md) | 后端分层架构（DDD）与多数据库支持 | 后端 | 一期验收 | T-022 | src 全部、tests |
| [T-024](T-024-code-comments.md) | 代码注释补齐，去掉代码里的任务号 | 后端 + 调用端 + 前端 | 一期验收 | T-023 | 全部源码、检查程序 |
| [T-025](T-025-ui-layout-pass.md) | 页面显示问题全面检查和修复 | 前端 + 调用端 | 一期验收 | T-024 | `src/**/*.vue`、样式、演示程序 |
| [T-026](T-026-system-info.md) | 系统信息设置和公司 Logo | 后端 + 前端 | 一期验收 | T-025 | `Domain/SystemSettings`、`Application/SystemSettings`、`src/pages/SettingsPage.vue` |
| [T-027](T-027-ui-feedback.md) | 界面反馈修复：模板树拖宽、素材格子、工作台滚动条、下拉框、登录页公司信息 | 前端 + 后端 | 一期验收 | T-026 | `src/**/*.vue`、样式、`Domain/SystemSettings` |
| [T-028](T-028-font-upload.md) | 字体由客户上传（ADR 0011） | 后端 + 前端 | 一期验收 | T-027 | `Rendering/Fonts`、`Application/Resources`、`src/pages/AssetsPage.vue` |
| [T-031](T-031-functional-integration.md) | 最新代码上的隔离功能与页面修复整合 | 后端 + 前端 | 一期验收 | 固定 0456cb4 | Logs、Persistence、页面、检查程序 |
| [T-032](T-032-layout-integration.md) | 页面与功能修复整合、缩放折叠及干净克隆回归 | 后端 + 前端 + 调用端 | 一期开发 | T-025、隔离候选 091c4e1 | Logs、Persistence、页面、浏览器回归 |
| [T-029](T-029-segmented-review.md) | 分段按钮页面补丁独立审阅 | 前端 | 一期开发 | T-032 | style.css、浏览器回归 |
| [T-030](T-030-page-compatibility.md) | 固定最新版本的页面与恢复兼容整合 | 后端 + 前端 | 一期开发 | T-025–T-028、1b5ec3a | 页面、Logs、Persistence、回归 |
| [T-033](T-033-logo-icon.md) | Logo 图标（顶栏、浏览器标签页）和 WebP 格式 | 后端 + 前端 | 一期验收 | T-028、T-030 | `Application/SystemSettings`、`src/pages/SettingsPage.vue` |
| [T-034](T-034-template-list-api.md) | 开放接口列出可用模板，SDK 增加 ListTemplatesAsync | 后端 + 调用端 | 一期验收 | T-009、T-011 | `Application/OpenApi`、`src/LabelService.Client` |
| [T-035](T-035-demo-batch-rows.md) | 演示程序：高分屏显示、刷新模板列表、批量数据由调用方给全 | 调用端 | 一期验收 | T-034 | `samples/LabelService.Demo.WinForms` |
| [T-036](T-036-template-tree-scale.md) | 模板多了以后的模板树和模板库：分组截取、搜索平铺、最近打开、分页 | 前端 | 一期验收 | T-027 | `src/components/admin/TemplateTree.vue`、`src/pages/TemplatesPage.vue` |
| [T-037](T-037-fixed-page-head.md) | 页面框架：标题和筛选固定，列表自己滚动 | 前端 | 一期验收 | T-036 | `src/page-frame.css`、`src/pages/` |
| [T-038](T-038-auto-defaults.md) | 字段的自动值（当前日期、有效期、批量序号、调用方等） | 后端 + 前端 + 调用端 | 一期验收 | T-034 | `Rendering/Validation`、`src/model/autoValues.ts` |
| [T-039](T-039-field-panel-insert-only.md) | 设计页的“字段”只负责放到画布上，字段统一在“数据源”页编辑 | 前端 | 一期验收 | T-038 | `src/components/FieldList.vue` |
| [T-040](T-040-demo-print-code.md) | 演示程序的调用代码写全：生成之后发送到打印机 | 调用端 | 一期验收 | T-035 | `samples/LabelService.Demo.WinForms` |
| [T-041](T-041-simpler-print-code.md) | 调用代码的打印写简单，PDF 加直接发文件的写法 | 调用端 | 一期验收 | T-040 | `samples/LabelService.Demo.WinForms` |

“主要目录”是相对于对应部分（`frontend/`、`backend/`、`client/`）的路径；后端的 Rendering、Server 指 `backend/src/LabelService.Rendering`、`backend/src/LabelService.Server`（T-023 起 Server 改名为 `LabelService.Api`，原来的模板存储、鉴权、日志等拆到 Domain、Application、Infrastructure，见 ADR 0010；已完成任务的“主要目录”保留当时的写法）。跨两个部分的任务（T-014、T-016、T-018、T-020）建议按部分拆成子任务分别认领；T-018 是契约变更，后端和调用端的契约副本要在同一个提交里改齐。

新增任务：复制 [_template.md](_template.md)，编号取当前最大值加 1，并在上表追加一行（只追加，不改别的行）。
