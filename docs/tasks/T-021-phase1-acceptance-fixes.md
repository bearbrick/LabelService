# T-021 一期验收问题修复与总体 review

状态：待验收
负责人：Claude 2026-10-10
里程碑：一期验收
依赖：T-001～T-019
设计书：§9 生成性能与监控、§8 开放接口、§4 A4 拼版；ADR 0006、0007、0009

## 目标

按独立验收（Codex，2026-10-10）指出的一期问题逐项修复、补需求缺口和验证，再做一期最终总体 review。验收材料在仓库外（`C:\Users\dukun\Documents\Codex\2026-10-09\task\acceptance`），这里只记结论、命令和本仓库里的证据。

## 范围

可以新建、修改的文件：

- `backend/tests/LabelService.Benchmarks/`：门禁 fail-closed、恢复完整基准覆盖（不改既有基线、不放宽门槛）
- `backend/src/LabelService.Server/Endpoints/`：真实计时口径（到写完最后一个字节）
- `backend/src/LabelService.Rendering/`：PDF 中文 ToUnicode（复制、搜索出来的字要对）；单张性能
- `backend/src/LabelService.Server/Data/`、`Templates/`：旧库升级的数据转换、迁移前备份和失败恢复
- `frontend/src/`：拼版设计器缺口（模板详情的可用版式）、真实交互验证
- 后端、前端、调用端的检查程序；`docs/`：如实记录验证结果和未决问题；`docs/reviews/`：总体 review 记录

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `AGENTS.md`（第 1 节当前阶段）
- `docs/tasks/README.md`（任务索引追加一行）
- `docs/performance.md`、`docs/contracts.md`（计时口径、实测结果）

## 验收标准

- [x] 基准门禁 fail-closed：构建失败、0 项执行、基线项缺席都不通过；恢复拼版 PNG 基准；不改基线重跑全门禁
- [x] 调用日志、指标的总耗时算到写完最后一个字节，有独立验证
- [x] PDF 里中文的 ToUnicode 正确（日、寸、毛等不再变成康熙部首），有检查
- [x] 旧库（AddApiClients 版本）升级不丢模板分类和调用方授权；迁移前自动备份，中途失败能恢复，有故障注入验证
- [x] 单张 50 并发持续压测：如实测量；达不到时给出证据和需要的决定，不改门槛
- [x] 拼版设计器：模板详情列出可用版式；真实鼠标拖拽的交互验证
- [x] ADR 0007、0009 等未采纳事项如实标注，列出需要用户决定的最小问题
- [x] 一期总体 review 记录（权限、错误处理、任务缺口、测试有效性）
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 二期 T-020；推送 GitHub；安装证书、凭据、新服务；真实打印机和生产数据；修改既有性能基线或放宽门槛

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

### 2026-10-10 · Claude

**做了什么**（逐项结论、权限和测试有效性的 review 见 `docs/reviews/phase1-review.md`，原始输出见 `docs/reviews/phase1-evidence/`）

- 基准门禁 fail-closed（`GateCoverage.cs`、`BaselineGate.cs`）：先用 BenchmarkDotNet 列出应跑的基准，再只认成功的结果；构建失败、过滤后为空、缺结果、不带过滤时缺基线项，都退出码 3、不写基线；打印中位数和 P95；`--self-test`。拼版基准恢复 PDF、PNG 两种格式。
- 计时（`Endpoints/RenderCall.cs`）：调用日志、指标、慢调用在 `HttpResponse.OnCompleted` 按写完最后一个字节计时，客户端中途断开记 `aborted`；`Server-Timing` 头只到写头为止（文档写明）。新检查 `TimingChecks`。
- PDF ToUnicode（`Rendering/Fonts/RadicalCmap.cs`）：加载字体时从 cmap 去掉和汉字共用字形的部首码位，文本里的部首字符排版前换成汉字。检查 `PdfChecks.CheckToUnicode`。
- 旧库升级（`Server/Data/DatabaseUpgrade.cs`、`LegacyUpgrade.cs`）：迁移前备份、失败恢复并拒绝启动；分类和按模板的授权换算成业务分组，每个调用方的可用模板升级前后一致。检查 `UpgradeChecks`（含故障注入）。
- 单张性能（`Rendering/Output/PngWriter.cs`）：PNG 按内容选 8 位灰度 / RGB / 1 位，zlib 最快档；无损。检查 `PngChecks`。压测脚本 `single.js` 默认改成 50 个并发持续 60 秒，新增 `log-report.cs`。
- 权限（review 发现）：调用方管理按业务范围限制（`Admin/ClientEndpoints.cs`、前端 `ClientsPage.vue`），检查 `ClientScopeChecks`。
- 前端：模板详情的“可用版式”（`pages/WorkspacePage.vue`）；拼版设计器用真实鼠标拖动、真实 HTML5 拖放复核。
- 文档：`docs/performance.md`（计时口径、实测结果、门禁规则）、`docs/contracts.md`、`docs/admin-api.md`、`backend/tests/load/README.md`、`backend/deploy/README.md`；T-010、T-016、T-019 交接记录如实补充。

**验证**

- 每个新检查都做了“去掉修复后必须失败”：门禁（错误过滤、模拟构建失败 → 退出码 3）、计时（修复前日志 36.2 ms → 修复后 853.7 ms）、ToUnicode（复现 日→U+2F47、寸→U+2F28、毛→U+2F51）、升级（去掉保护后故障注入检查失败）、调用方范围（去掉保护后失败）、PNG（灰度通道改 1 个最低位就失败）。
- 完整基准门禁两次（基线不变）：18 项全部执行、16 项比较通过；PNG 各项 −79%～−86%，PDF、ZPL +2%～+12%（波动）。
- k6 50 并发持续 60 秒（本机绑核）：修复前 P95 253 ms 不通过；修复后绑 2 个物理核到最后一个字节 P95 58.2 ms、P99 67.6 ms，绑 4 个物理核 P95 63.7 ms、P99 98.1 ms，错误 0。
- 完整 `verify.ps1` 通过（2026-10-10T13:06Z 开始）：后端检查 420 项、调用端检查 41 项（含对真实服务的端到端检查）、前端检查 69 项和打包，两个解决方案的格式检查。

**没做 / 环境阻塞 / 需要决定**

- 环境阻塞：4 核 8 GB 目标机压测、真实 Prometheus / Grafana / 告警、真实打印机、干净 Win10、Docker、拼版设计器的逐帧帧率（浏览器窗格隐藏时 rAF 暂停）。
- 需要用户决定：见 review 第 7 节（ADR 0007、ADR 0009、`sys.user` / `sys.role` 权限边界、旧库换算方式、T-018 的两项补充、要不要收紧基线、操作日志是否按业务范围过滤）。
- 按验收要求没有改基线、没有放宽门槛、没有推送。
- `verify.ps1` 第一次失败：调用端编译时有一个正在运行的 `LabelServiceDemo.exe`（net10.0-windows Debug）占着输出目录里的 DLL（环境问题，不是代码问题）；那个进程退出后重跑。
