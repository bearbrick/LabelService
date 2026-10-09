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

“主要目录”是相对于对应部分（`frontend/`、`backend/`、`client/`）的路径；后端的 Rendering、Server 指 `backend/src/LabelService.Rendering`、`backend/src/LabelService.Server`。跨两个部分的任务（T-014、T-016、T-018、T-020）建议按部分拆成子任务分别认领；T-018 是契约变更，后端和调用端的契约副本要在同一个提交里改齐。

新增任务：复制 [_template.md](_template.md)，编号取当前最大值加 1，并在上表追加一行（只追加，不改别的行）。
