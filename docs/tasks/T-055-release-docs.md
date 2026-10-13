# T-055 定版前文档与测试入口整理

状态：进行中
负责人：Codex 2026-10-13
里程碑：定版前维护
依赖：现有功能及 T-051～T-054 已实现
设计书：§scope、§arch、§nfr、§sdk、§demo、§plan（仅核对与同步实现）

## 目标

按用户要求，将当前源码仓库的介绍、运行部署、SDK 集成、AI 维护和测试说明整理成可供定版复核的文档。清除过时、重复或无引用的一次性文件，保留有效回归和验收证据；完整验证后沿用用户已确认的 main 合并与推送授权交付 GitHub。

## 范围

- 根 README.md；frontend/README.md、backend/README.md、client/README.md；两个 SDK 包 README。
- docs/README.md、release-readiness.md、testing.md、roadmap.md（新）；architecture.md、contracts.md、admin-api.md、performance.md 及设计书：只修正文档和实现不一致或过时的描述，不改契约与设计。
- backend/deploy/README.md、assets/fonts/README.md、tests/load/README.md：操作和入口说明。
- docs/reviews/README.md（新）及历史报告的当前状态提示；docs/tasks/README.md 和本任务；历史任务保留，不修改他人进行中的 T-030。
- frontend/tests/README.md（新），合并现有三个测试 README 并删除无独立内容的旧入口；引用到旧入口的前端 README、历史报告与非进行中任务仅更新链接。保留实际测试脚本、品牌检查页与源代码回归。
- frontend/package.json：把可离线运行的现有真实模块回归纳入 check，不新增依赖。
- tools/check-docs.mjs（新）和 verify.ps1：检查被跟踪 Markdown 的本地文件链接，文档归档整理记录。
- AGENTS.md：只同步当前阶段、文档/测试入口，保留硬性规则；其他 AI 入口保持指向 AGENTS。
- 若有经引用和内容核对证实过时的重复文档/截图/测试临时文件，可删除或合并，具体清单先追加交接记录，不删除有效测试或生产数据。

## 共享文件

- AGENTS.md、verify.ps1、frontend/package.json、docs/tasks/README.md；docs/contracts.md 如需更正文案仅作同步，不改协议。

## 验收标准

- [ ] 当前 README 内容与真实功能、版本、运行命令一致；历史路线与证据明确标识，不冒充当前验收结果。
- [ ] 文档与测试入口集中，删除清单和引用检查有记录；保留有效自动化测试、ADR、契约和已评审设计。
- [ ] 全量 verify.ps1 通过，新增文档链接检查和现有离线模块回归可运行。
- [ ] 现场打印、目标服务器及性能门禁异常如实列出，不把预览版本或待验收任务改成正式完成。
- [ ] 任务交接记录完整，main 正常快进合并并推送，远端 SHA 一致，本地运行环境恢复。

## 不做

- 不改业务逻辑、数据库、契约版本、依赖版本、渲染性能基线；不自动发正式版或上传 NuGet 包。
- 不删除历史任务和必要验收证据，不替用户宣布现场验收完成；不修改 T-030 的进行中任务文件。
- 不清空用户运行数据、上传字体、Logo 或本地可用 SDK 包；不强推或改写历史。

## 交接记录

- 2026-10-13 · Codex · 用户确认项目可能进入定版，要求整理 README、文档和 AI 测试文件，并最终推送。沿用本会话用户对 main 合并和推送的明确确认，不再重复询问。共享文件如上登记。初查发现根 README 的骨架阶段、SDK 4.6.2、演示程序最小可用等已过时；将建立当前文档入口与定版核对页，同时保留现场验证和历史性能门禁异常。
