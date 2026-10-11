# T-032 合并页面修复与功能恢复并验证

状态：待验收
负责人：Codex 2026-10-11
里程碑：一期验收
依赖：T-025、T-026
设计书：§designer、§demo；不改契约、设计、Rendering 或系统 DPI

## 目标

在独立分支基于 087aeba 整合 091c4e1 的有效修复，保留页面和 WinForms 布局改进、源码登记、SQLite 恢复及异步请求竞态修复。原目录只读，手测数据保持不变。

## 范围

- 从 091c4e1 合入已验证的 .gitignore、backend 源码及检查、frontend 功能及 UI 修复、任务和评审文档。
- 协调 frontend/src/components/admin/TopBar.vue、TemplateTree.vue、layout-recovery.css，保留 WorkspacePage 的双方逻辑。
- frontend/src/components/admin/NavToggle.vue、frontend/src/editor/stageViewport.ts、sheetStage.ts、相关 composables/纯状态模块及 frontend/tests/**：新增行为的可重复回归。
- frontend/README.md、docs/reviews/T-032-layout-integration.md 和本任务记录。
- 隔离环境和工作区证据目录：干净克隆验证、真实浏览器尺寸/缩放/长内容和 WinForms 自检及截图。

## 共享文件

- docs/tasks/README.md：追加本任务一行；合入 091c4e1 既有任务记录。独立提交。
- .gitignore：原样保留 091c4e1 已验证的源码登记规则，不新增依赖。
- frontend/src/main.ts：保留 091c4e1 的布局恢复 CSS 导入。

## 验收标准

- [x] 保留双方有效修复，文本、重复属性和 CSS 冲突已解决
- [x] 新增折叠与缩放自动回归，旧请求竞态及 SQLite 恢复检查保持通过
- [x] 干净克隆完整 verify.ps1 通过，测试不依赖忽略源码或旧构建产物
- [x] 真实本机浏览器常用尺寸、原生 125%/150% 缩放和长内容回归通过
- [x] 1100px 和 1101px 的窄屏提示、画布和焦点边界已复查
- [x] WinForms net48/net10 自检、可验证布局截图通过；高 DPI 未测明确记录
- [x] 自建进程已清理，手测数据保留；原目录活动与改动已复查，发现新任务时明确记录整合阻塞
- [x] 候选提交、证据和可回滚本地整合方案已交接

## 交接记录

2026-10-11 · Codex：收到用户授权，在新隔离副本工作；原项目固定 087aeba 且干净，Claude 会话最后活动 14:03:48 UTC。不改原目录、不推送、不修改 OS 设置。基于 T-025 与 T-026 的设计和契约，按已有行为处理冲突。

2026-10-11 · Codex：独立候选实际代码 9b22371 在全新克隆完整验证 609 项；实际 Chrome 行为 27 项、画布编辑 24 项、长内容边界 68 项通过，常用页面/弹窗/可复用回归/原生缩放共 338 张截图，另有四张精确边界截图。两框架各 12 项自检通过。关键截图随仓库提交，完整证据位于同级 evidence/phase16。自建进程退出，端口释放，手测数据保持原元数据。

原目录复查发现 Claude 已启动新的 T-026-system-info 并仍在修改。候选没有应用原目录；双方 T-026 编号重名与新功能合并留给该任务稳定后的第三个隔离候选处理。详见 [整合报告](../reviews/T-032-layout-integration.md)。OS 高 DPI、真实打印机、PostgreSQL 和目标设备未验收。候选待用户验收，未合入原目录或推送。

兼容编号记录：本文件来自原隔离候选的 T-028-layout-integration，为保留原 T-028-font-upload 而改为 T-032；原提交 SHA 和历史截图目录不变。
