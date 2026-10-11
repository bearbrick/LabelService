# T-031 最新代码上的隔离功能与页面修复整合

状态：待验收
负责人：Codex（独立副本）
里程碑：一期验收
依赖：固定 0456cb4；T-024 的性能验收仍独立待解决
设计书：现有管理接口、数据库升级规则与页面原型

## 目标

在最新提交的独立副本移植 a2cbba2 中尚缺的功能与页面修复，保留模块拆分、注释检查和 1100px 设计器门槛，提供可复核候选及干净克隆测试。

## 范围

- 后端 Application/Logs、Domain/Logs 中未纳入版本控制的十个源码文件。
- Infrastructure/Persistence/DatabaseUpgrade.cs；检查程序 DatabaseProviderChecks.cs、UpgradeChecks.cs、UpgradeChecks.Initialization.cs、RepositorySourceChecks.cs。
- frontend/src/composables/useAvailableLayouts.ts、layout-recovery.css、main.ts。
- frontend/src/components/admin/TemplateTree.vue、TopBar.vue；pages/BusinessPage.vue、CallLogsPage.vue、ClientsPage.vue、OpLogsPage.vue、PresetsPage.vue、RolesPage.vue、UsersPage.vue、WorkspacePage.vue。
- frontend/tests/available-layouts.ts、browser-boundaries.mjs、README.browser-boundaries.md。
- 本任务文件、docs/reviews/T-031-functional-integration.md。

## 共享文件

- .gitignore：将运行时日志目录排除限定到实际目录，允许纳入 Logs 命名空间源码。
- backend/tests/LabelService.Backend.Checks/Program.cs：保留原有检查，登记新增源码与初始化恢复检查。
- frontend/tests/checks.ts：保留注释和文件规模检查，登记竞态检查。
- backend/README.md、frontend/README.md：在最新模块说明上补充修复和复核说明。
- docs/tasks/README.md：仅追加本任务索引。

## 验收标准

- [x] 缺失修复按职责移植，四处冲突手工解决，无旧架构覆盖。
- [x] 干净克隆 verify.ps1 通过，含注释、格式、构建、SQLite 恢复、请求竞态及真实 HTTP SDK 检查。
- [x] 用户电脑 Chrome 在独立配置和合成数据上复核页面、真实缩放与重构后的画布交互。
- [x] 原仓库、手测数据库、用户与 Claude 进程不受影响；自建服务与临时数据库清理。
- [x] 形成候选提交及可复核证据；性能门禁保留未通过结论且不修改基线。

## 不做

不改原目录或手测数据库，不发布，不重放 Claude 指令，不改变渲染实现或性能基线，不新增依赖或改变接口契约，不设置自动化。

## 交接记录

- 2026-10-11 10:17 UTC · Codex · 功能与页面候选完成，四处冲突手工整合，保留 T-024 模块与检查规则；新干净克隆 609 项通过（后端 481、SDK 47、前端 81）。本机 Chrome 全页面 90 图、真实缩放 120 图、长内容 58 图/68 检查、扩展 35 图、可复用脚本 35 组合、画布真实交互 24 检查均通过。自建服务与临时 LocalDB 已清理，原仓库与手测 DB 元数据不变。详情见 docs/reviews/T-031-functional-integration.md 和仓库外 evidence/phase10。性能独立未通过；静态语法对照不能证明 JIT/性能等价，基线未改，不运行新的基准。等待候选审阅，不表示整个系统已完成。

- 2026-10-11 · Codex · 用户明确要求现在在新隔离副本整合，因此可以跨后端与前端移植，并在 T-024 性能尚待验收时继续本任务。固定 0456cb4 与 6818afe 的差异只有 T-025 任务资料。共享文件改动已登记，分别提交。浏览器使用此电脑 Chrome 的独立配置；先前截图不作为新候选的通过证据。

兼容编号记录：本文件来自原隔离候选的 T-026-functional-integration，为保留原 T-026-system-info 而改为 T-031；原提交 SHA 和历史证据不变。
