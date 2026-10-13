# T-055 定版前文档与测试入口整理

状态：待验收
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
- frontend/package.json：保留已有离线回归 check，给三个真实浏览器脚本增加显式可选入口，不新增依赖。
- tools/check-docs.mjs（新）和 verify.ps1：检查被跟踪 Markdown 的本地文件链接，文档归档整理记录。
- AGENTS.md：只同步当前阶段、文档/测试入口，保留硬性规则；.github/copilot-instructions.md 删除已过时的重复规则，仅指向 AGENTS；其他 AI 入口保持指向 AGENTS。
- 若有经引用和内容核对证实过时的重复文档/截图/测试临时文件，可删除或合并，具体清单先追加交接记录，不删除有效测试或生产数据。

## 共享文件

- AGENTS.md、verify.ps1、frontend/package.json、docs/tasks/README.md；docs/contracts.md 如需更正文案仅作同步，不改协议。

## 验收标准

- [x] 当前 README 内容与真实功能、版本、运行命令一致；历史路线与证据明确标识，不冒充当前验收结果。
- [x] 文档与测试入口集中，删除清单和引用检查有记录；保留有效自动化测试、ADR、契约和已评审设计。
- [x] 全量 verify.ps1 通过，新增文档链接检查和现有离线模块回归可运行。
- [x] 现场打印、目标服务器及性能门禁异常如实列出，不把预览版本或待验收任务改成正式完成。
- [x] 任务交接记录完整，main 正常快进合并并推送，远端 SHA 一致，本地运行环境恢复。

## 不做

- 不改业务逻辑、数据库、契约版本、依赖版本、渲染性能基线；不自动发正式版或上传 NuGet 包。
- 不删除历史任务和必要验收证据，不替用户宣布现场验收完成；不修改 T-030 的进行中任务文件。
- 不清空用户运行数据、上传字体、Logo 或本地可用 SDK 包；不强推或改写历史。

## 交接记录

- 2026-10-13 · Codex · 已完成交付推送：main 从 5ca2138 正常快进到 1694eaa，main 与任务分支一起正常推送；git ls-remote 核对 main 与本地一致，工作区干净。localhost:5080/health/ready 与 localhost:5173 均返回 200，net48 演示已恢复。本条完成记录另作任务分支文档提交后同步 main，不改产品或验证结论。用户后续复核入口为 docs/README.md 和 docs/release-readiness.md，正式发版与现场验收状态保留待确认。

- 2026-10-13 · Codex · 文档整理和自检完成，待用户复核。根 README 从过时的骨架/排期介绍收敛为当前能力与快速使用；新增 docs/README、release-readiness、testing、roadmap 和 reviews/README，分别承担当前入口、定版核对、测试、未来候选、历史证据。同步三个部分与两份 SDK README，修正最低 Framework、preview.5、完整演示程序、字段来源和 USB/静默打印说明。补齐内置 BOX_LABEL 示例必填字段，文件与集合打印示例分开，避免复制时重复提交。开发示例用环境变量取 Key；不声称包已在公共 NuGet 发布。
  - 清理：删除三个已合并的前端测试 README 和一张经内容哈希确认重复的截图；保留所有实际回归脚本、唯一截图、历史任务、ADR 与已评审设计。历史报告加来源限制，14 个仓库外临时证据超链接改为原路径文字；T-030 进行中任务未改。Copilot 入口删除旧分层/net462 重复规则，统一指向 AGENTS；AGENTS 硬性规则不变，仅更新阶段与入口。
  - 测试：新增零第三方依赖的 tools/check-docs.mjs，含断链、代码块、引用定义、空格、非法编码与仓库边界自检。verify.ps1 全量模式增加文档检查；107 个 Markdown、190 个本地文件目标通过。三个真实浏览器脚本增加可选 npm 命令，保留隔离服务/合成数据要求，不自动安装依赖、不混入离线默认检查；脚本语法检查通过，本次未改界面逻辑，不重跑真实浏览器或桌面 smoke。全量 verify 通过：后端 529、调用端 137（真实隔离服务）、前端 117，日志 artifacts/release-docs/verify.log。
  - 操作：内置备份源码只复制数据库与 assets，部署说明和定版清单明确 fonts、branding、签名密钥及配置须额外备份，不声称已有自动全目录备份。正式 SDK、真实设备、目标服务器和历史性能门禁仍列待核对；未改基线或重新运行基准。
  - 设计书仅同步现有 ADR 0010 的分层/多实例限制、当前 SDK 演示版本及已用 Vue 3；标明 CI 尚未接入自动流水线，未改设计本身。产品源码、数据库和依赖版本不变，preview.5 不自动升正式版。验证后后端、前端与 net48 演示恢复运行，进程记录 artifacts/local-run/processes.json，后续按用户授权正常快进 main 并推送。

- 2026-10-13 · Codex · 用户确认项目可能进入定版，要求整理 README、文档和 AI 测试文件，并最终推送。沿用本会话用户对 main 合并和推送的明确确认，不再重复询问。共享文件如上登记。初查发现根 README 的骨架阶段、SDK 4.6.2、演示程序最小可用等已过时；将建立当前文档入口与定版核对页，同时保留现场验证和历史性能门禁异常。

- 2026-10-13 · Codex · 清理范围确认：合并 frontend/tests/README.browser-boundaries.md、README.layout-behavior.md、README.segmented-browser.md 到同目录 README.md，删除这三个旧入口。只有 frontend/README.md 存在 Markdown 链接，更新到新入口；T-031 里的文件名是历史范围描述，不改原记录。三个浏览器脚本均需隔离服务和 Playwright，不是可离线的纯模块回归，保留并增加可选 npm 命令，不加入默认 check。发现 Copilot 入口仍写旧 Server 分层和 net462，改为单一 AGENTS 引用。备份源码实际仅包含 SQLite 数据库和 assets，部署文档会补充 fonts、branding、签名密钥的独立备份要求，不借文档声称代码已自动备份这些目录。

- 2026-10-13 · Codex · 截图内容哈希核对发现 docs/reviews/t028-evidence/demo-min-preserved.png 与 t025-evidence/demo-960x600-after.png 字节一致；保留 T-025 原证据，删除 T-028 重复副本，T-032 报告链接转向保留文件，不改其历史结论。新链接检查发现 T-031、T-032 的 14 个仓库外工作区链接不可随 GitHub 克隆访问，将其改为明确标注的历史路径文字，仍保留原文件名与说明。所有唯一截图和实际回归脚本保留。
