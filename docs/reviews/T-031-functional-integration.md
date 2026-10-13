# 最新代码隔离功能与页面整合验收记录

> 历史评审：结论只对应文中日期和固定提交，保留用于追溯。当前状态见 [定版清单](../release-readiness.md)，本页不作为最新 main 的通过证明。

本候选基于 `0456cb4387370d74e1e524b54c6a487f20895c24`，按职责移植 `a2cbba23cbe1e9996ca60f6cd70dc8b3c3c11ca5` 中尚缺的修复。功能与页面回归通过，性能门禁仍独立未通过，不能据此宣称整个系统完成。

## 固定版本和改动边界

- `0456cb4` 相对 `6818afe` 只有 `docs/tasks/README.md` 和新增 T-025 任务文件变化，没有产品代码变化。
- 候选分支：`ai/codex/T-031-functional-integration`；实际全量测试提交：`49bf64a591f1fc51b4efab245efc4c5d8fca03b0`。后续交接提交仅补任务与本验收记录，产品与检查代码相同。
- 仓库：`phase1-integration-6a6b/integration-0456-functional`；干净克隆：同目录下 `integration-0456-verification`。克隆源码全部来自 Git，仅复用被忽略的离线依赖和字体缓存。
- 原仓库 `D:\Work\test\example\LabelService` 保持 `0456cb4`、工作区干净。本机手测 `manual-testing/data/label.db` 的大小 135168 字节、最后修改时间 `2026-10-10T09:42:20.5370783Z` 不变。
- 未发送 Claude 消息、点击批准、修改原项目、停止用户进程或设置自动化。浏览器使用此电脑已安装 Chrome 的独立上下文和配置，访问自建 localhost 服务及新 GUID 合成数据目录。

## 修复与四处冲突处理

1. 限定运行日志忽略规则，将十个 Domain/Application 的 Logs 源文件纳入 Git。内容与原工作目录被误忽略的源码一致；新增源码完整性检查避免“本机能编译、干净克隆缺文件”。
2. SQLite 每次有待执行迁移时，先保存可恢复快照；首次空库也创建空库快照，没有迁移历史的已有库不能跳过备份。快照名带 GUID，失败恢复并拒绝启动，重试不覆盖先前快照。
3. 真实数据库检查将主库和日志库连接都指向本次 GUID 临时库，避免使用其他日志库。
4. 提取 `useAvailableLayouts`，模板、尺寸变化和组件卸载统一失效旧请求；旧成功或旧失败都不能覆盖当前列表。
5. 长名称有完整 title，模板树名称允许两行；表格使用自身可聚焦滚动区域和固定列宽；菜单、弹窗和长内容可滚动。模板工作区保留至少 320px 内容区，滚轮可到达标签页和窄屏提示；设计器保留 1100px 门槛。

手工解决 `backend/README.md`、后端检查入口 `Program.cs`、`frontend/README.md`、`WorkspacePage.vue` 四处冲突。README 在最新模块说明上补充修复；检查入口保留已有架构、注释、规模检查；工作区只替换旧版式请求 watcher。没有用旧完整文件覆盖新架构。

T-024 的 `designerState/History/Template/Elements/Fields`、`stageViewport`、画布组合函数、`tests/docs.ts`、AGENTS 注释规则及渲染代码保持最新版本。新增检查的注释已按同一门禁补齐，没有跳过检查或降低要求。

## 可核验测试

全新克隆在 `2026-10-11 10:06:08–10:08:01 UTC` 执行 `verify.ps1`，退出码 0、Git 工作区干净；构建和格式检查通过。

| 检查 | 结果 |
| --- | --- |
| 后端 | 481 项通过，含全部 XML 注释、分层和源码完整性检查 |
| 调用端 SDK | 47 项通过，含临时服务真实 HTTP 端到端验证 |
| 前端 | 81 项通过，含多行 JSDoc、500 行限制和 10 个异步竞态场景 |
| 合计 | 609 项通过 |
| SQLite 新增故障注入 | 20 项通过：空主库、无迁移历史但有数据的库、首次日志库、备份目录失败、恢复及重试 |
| 数据库 | SQLite、真实 LocalDB 临时库通过；前后临时库清单均为 0；PostgreSQL 实例未配置 |

浏览器回归全部重新执行，历史截图不作为此候选的通过证据：

| 本机 Chrome 回归 | 结果 |
| --- | --- |
| 18 个页面 × 5 种尺寸 | 90 张截图，无文档撑宽、逃逸控件及页面异常 |
| 菜单、全套新建弹窗、受限权限、重复模板切换 | 35 张截图，断言通过、无页面异常 |
| 超长姓名、角色、AppId、表格及弹窗 | 58 张截图、68 项检查通过 |
| 候选内可复用边界脚本 | 35 个组合通过，确认实际夹具单元格存在 |
| 真实 Chrome 125%/150% 缩放 | 120 张截图通过，覆盖滚轮到达标签页、设计区、字段/接口内容底部及窄屏提示 |
| 重构后画布操作 | 24 项通过，另存 2 张截图 |

缩放通过 `chrome://settings/appearance` 设置，记录物理截图尺寸、CSS 视窗尺寸、DPR 与 `visualViewport.scale`；没有使用 CSS zoom 或 `deviceScaleFactor` 模拟。操作系统显示缩放仍未测。

画布检查使用真实按钮、键盘、鼠标拖动、变换手柄、HTML 拖放和滚轮，验证元素新建、选择、几何调整、文本与字段补全、撤销重做、复制、锁定、微调、视口缩放与平移、字段拖入、快照、真实草稿保存及页签切换后的重新挂载。保存后的字段和四个元素通过 HTTP 再读取核验。测试脚本初次的模块定位及补全选择器错误已修正，最终 24 项全通过且无浏览器异常。

## 性能证据与剩余阻塞

性能门禁保持未通过。本次未运行新的性能基准，未改变 Rendering、基准代码或 `baseline.json`；本机基线文件 SHA-256 保持 `7d852a0732f7e9272b0843a4116f7a3162dfac66c6ff8085df993247dc17e614`。先前门禁和正反顺序 A/B 中的回退仍是待解决证据。

未发现活跃的 `LabelService.Benchmarks` 进程时，在独立工具中对旧版 `6a6b329` 与最新版本做 Roslyn 语法及构建配置对照：

- 203 个带实现的方法或构造函数可对应；在明确映射抽出的类型名称和辅助方法限定符后，202 个方法体的语法 token 相同，`TextLayout.Arrange` 剩余差异仅为局部变量 `text0` 改名 `measured`。
- 19 个对应方法的所属类型或可见性发生变化，包括抽出的文本分行和字体 cmap/sfnt 辅助方法。该事实可能影响编译与 JIT 条件，不能凭语法相同认定运行性能等价。
- SDK 固定文件、后端编译设置、包版本、Rendering 项目和基准项目文件相同；新增 `backend/tests/Directory.Build.props` 启用 XML 文档。基线 Git 内容相同，字体哈希已记录。
- 此对照未比较最终 Release IL、JIT 内联、所有字段初始化或完整基准环境，也没有证明回退仅由机器负载造成。下一步仍需在稳定条件下固定完整 18 项基准覆盖和构建产物，结合 IL/JIT、缓存及分配证据定位；不能重写基线来放行。

还未验收的范围包括性能门禁、完整目标硬件的资源上限与吞吐要求、操作系统显示缩放、真实 PostgreSQL 和实际打印设备。这里的功能与页面通过不代表这些项已通过。

## 证据与清理

详细证据均在工作区外置目录 phase10（历史工作区路径：`../../../evidence/phase10/verification-summary.json`）：全量日志（历史工作区路径：`../../../evidence/phase10/verify-clean-clone.log`）、干净克隆结果（历史工作区路径：`../../../evidence/phase10/full-verification-result.json`）、真实缩放（历史工作区路径：`../../../evidence/phase10/real-zoom-results.json`）、画布交互（历史工作区路径：`../../../evidence/phase10/canvas-regression-results.json`）、语法对照（历史工作区路径：`../../../evidence/phase10/source-token-comparison.json`）、构建条件（历史工作区路径：`../../../evidence/phase10/build-condition-comparison.json`）。这些均为本次独立执行证据，区别于 Claude 的自述。

自建 Vite/API 实例已关闭，Chrome 测试上下文已关闭，5089/5183/5189 无监听，生成的 LocalDB 数据库已删除。仅保留自建合成 SQLite 数据、截图与测试日志供复核。

兼容编号记录：本文件来自原隔离候选的 T-026-functional-integration，为保留原 T-026-system-info 而改为 T-031；原提交 SHA 和历史证据不变。
