# 页面与功能修复整合候选

> 历史评审：结论只对应文中日期和固定提交，保留用于追溯。当前状态见 [定版清单](../release-readiness.md)，本页不作为最新 main 的通过证明。

2026-10-11，Codex。独立分支 `ai/codex/T-032-layout-integration`；稳定起点为 `087aeba48df486acc44b3bdbd9fba47502d419f6`，完整合入 `091c4e1d6a996ace8e19ddde25ef5e9e5a7d8097`。实际代码与浏览器检查针对 `9b223710a76b6c386ab06904ab542e105ff3b567`；之后交付提交只增加本报告、截图及任务/README 文档。

## 结果与修复

- 保留 T-025 的 WinForms 最小窗口布局、前端长名称显示、自动折叠、画布适应窗口与手动缩放/平移保持行为。
- 合入 091c4e1 的十个 Logs 源文件登记、SQLite 备份与初始化恢复、跨数据库测试隔离和可用版式请求竞态修复。干净克隆无须从工作目录补拷贝任何源码。
- TopBar 的当前业务使用一个带完整名称提示的 `project-name`，保留菜单标签与用户提示，并协调原 `.biz-name` 宽度规则。
- TemplateTree 每个模板只保留一个 `:title`，保留名称、编码、尺寸提示及可访问名称；Vue 编译和构建通过。
- 删除重复的 1100px 设计器隐藏规则；业务名称芯片内部文字采用省略与悬停提示，圆点固定，其他芯片保持允许换行。
- 未改契约、Rendering 或 npm 依赖。WinForms 源码树与 087aeba 完全一致；Rendering 与 091c4e1 完全一致。

## 可核验测试

全新克隆 `integration-087a-final-verification` 从 Git 获取源码，仅显式复用已安装 node_modules 和授权字体。2026-10-11 14:41:10–14:42:52 UTC 的 `verify.ps1` 退出码 0：后端 481、SDK 47、前端 81，共 609 项通过，并完成所有构建。SQLite 与真实 SQL Server LocalDB 临时数据库检查执行；临时数据库前后清单保存。PostgreSQL 实际服务未配置。

浏览器使用此电脑已安装的 **Google Chrome**，由 Playwright `channel: chrome` 驱动自己的无头上下文与独立配置目录。未使用用户现有浏览器配置；未改变 OS DPI。

| 回归 | 实际结果 |
| --- | --- |
| 新增实际 Vue/Konva 缩放与折叠行为 | 27 项通过；手动展开保持、1440px 门槛、监听清理、适应/缩放/平移后 resize、重新适应、模板切换及卸载 |
| 1100/1101px 设计器边界 | 两类设计器各两个截图；1100px 提示且工具栏/画布隐藏、隐藏区域不获焦；1101px 可编辑且无横向溢出 |
| 常用窗口矩阵 | 18 页面 × 1366×768、1920×1080、1280×720、1366×600、1024×600，共 90 张截图，无页面异常或越界控件 |
| 弹窗、权限、滚动及反复模板切换 | 35 张截图；退出码 0，无页面异常或越界控件 |
| 长名称/说明、菜单、搜索、表格及用户边界 | 68 项断言、58 张截图，全部通过 |
| 仓库内可复用 browser-boundaries.mjs | 35 场景全部通过，无内容溢出或浏览器异常 |
| 原生 Chrome 125% / 150% | 1920×1080 与 1366×768，共 120 张截图全部通过；含长内容、菜单、弹窗、设计器与滚动可达性 |
| 真实画布编辑 | 24 项鼠标、键盘、拖放、撤销、隔离草稿保存及重新挂载检查通过 |
| WinForms .NET Framework 4.8 / .NET 10 Windows | 两框架各 12 项自检通过，退出码均 0；新截图和输出保存，进程退出 |

浏览器 100% 基线 DPR=1；125% 的 DPR=1.25，1920 CSS 视口变为 1536、1366 变为 1093；150% DPR=1.5，CSS 宽分别 1280 与 911，visualViewport.scale 始终为 1。该证据是浏览器原生缩放，不构成 Windows 125%/150% DPI 验收。设计器实际规则为 **CSS 宽度大于 1100px 才可编辑**；1100px 本身提示加宽。

初次完整验证曾因测试桥接调用不存在的 `SheetStage.zoomBy` 失败，改为 `zoomTo`；首次行为检查暴露模拟滚轮派发目标错误，改为实际 Konva 容器后通过。这两次失败日志保留。最后确切代码提交在另一个全新克隆中完整通过，未掩盖失败或以旧版本结果替代。

## 证据位置与截图

完整证据在隔离工作区同级 `evidence/phase16/`；汇总（历史工作区路径：`../../../evidence/phase16/validation-summary.json`）、全新克隆完整日志（历史工作区路径：`../../../evidence/phase16/verify-exact-head-clean-clone.log`）、行为断言（历史工作区路径：`../../../evidence/phase16/layout-behavior/layout-behavior-results.json`）、原生缩放校准与结果（历史工作区路径：`../../../evidence/phase16/real-zoom-results.json`）、长内容断言（历史工作区路径：`../../../evidence/phase16/ui-before-results.json`）、画布编辑断言（历史工作区路径：`../../../evidence/phase16/canvas-regression-results.json`）、两框架自检（历史工作区路径：`../../../evidence/phase16/demo-smoke-results.json`）。`ui-before-results.json` 沿用旧脚本默认文件名，本次执行提交为 9b22371，时间/提交见 `boundaries-execution.json`，不是修改前基线。

关键截图随仓库提交：

- [1100px 模板提示](t028-evidence/template-1100.png)、[1101px 模板可编辑](t028-evidence/template-1101.png)。
- [150% 长内容滚动后窄窗提示可达](t028-evidence/chrome150-long-template.png)、[125% 长说明表格](t028-evidence/chrome125-long-business.png)。
- [本候选 net48 自检布局](t028-evidence/demo-net48-default.png)、[本候选 net10 自检布局](t028-evidence/demo-net10-default.png)。
- [保留的 T-025 最小窗口实际截图](t025-evidence/demo-960x600-after.png)：六行和 number/必填列完整，生成选项通过左侧纵向滚动到达。此图来源于 Claude 14:03 UTC 前的已提交布局；本候选该源码树完全相同。默认窗口自检截图为本次重新执行，不把保留图称为新拍摄。

## 原目录新活动与应用限制

最终只读检查发现原目录并未保持空闲。Claude 于 14:31 UTC 恢复交互，用户于 14:39:19 UTC 提出公司 Logo 上传、系统标题、所属公司与版权设置。14:42:36/42 创建并登记 `T-026-system-info`；原目录在 `ai/claude/T-026-system-info`，HEAD 为 `14b46dc1e912cc965d140af2c5e439557df0b5a9`，且有 SystemSettings、图片处理、权限、存储及 API 等未提交改动。14:45 UTC 后仍有连续工具调用；该新功能没有完成验证。未见新额度错误，不推断下一次重置时间。

这不影响基于 087aeba 的独立候选验证，但阻止现在向原活动目录直接应用候选。保留双方新工作，待 Claude 稳定后在**第三个隔离分支**合并最新提交，再解决 TopBar/注册等重叠、`T-031-functional-integration` 与新 `T-026-system-info` 的编号重名，然后重新完整验证。当前候选不能被宣称已经包含新系统信息功能。

原目录与 Claude 本任务只读，未发送消息、点击批准、切分支或修改文件。手测 `manual-testing/data/label.db` 仍为 135168 字节，最后写入 2026-10-10 09:42:20.537078 UTC。自建 API 80916、启动器 2356、WinForms 自检 73968/42588 均退出；5183/5189 端口释放；合成测试数据目录保留。详见 `original-final-state.json`、`new-original-activity.json`、`process-cleanup-check.json`。

## 可回滚的本地整合方案

交付本地 Git bundle 和从 087aeba 到候选的二进制 diff。bundle 经 `git bundle verify`；补丁在新的 087aeba 克隆执行 `git apply --check`，然后只在该克隆 index 验证生成的树与候选一致。

1. 优先从 bundle 克隆一个新的评审目录，只操作该目录；原目录和其活动分支保持不动。查看候选代码、随仓库关键截图及外部证据。
2. Claude 新任务稳定后，将其确切提交导入第三个隔离克隆，在这里合并候选并解决上述代码/任务编号冲突。再次使用干净克隆完整验证，以及新 TopBar/系统设置的浏览器回归。
3. 只有确认原目录无正在运行的任务、干净且用户批准实际应用后，再备份当前分支、未提交/未跟踪和仍被忽略的源码及手测数据，按审定的合并提交整合。当前阶段不执行。
4. 评审失败时放弃第三个隔离分支或返回其保留的起点即可；不需要重写原目录历史。若以后已应用，则保留整合前分支和数据备份，以独立工作区检出旧分支检查/恢复。避免在活动原目录执行硬重置。

## 尚未验收

Windows OS 高 DPI、实际打印机、实际 PostgreSQL、目标 4 核 8GB 设备仍需对应环境。此前 091c4e1 的本机性能门禁证据保留在 phase12，本次未改 Rendering；不把该历史门禁当作本次新候选重新测得，也不把机器因素当作已证明根因。最终是否交付生产仍由原系统验收和新任务整合结果决定。

兼容编号记录：本文件来自原隔离候选的 T-028-layout-integration，为保留原 T-028-font-upload 而改为 T-032；原提交 SHA 和历史截图目录不变。
