# T-049 客户端程序图标

状态：待验收
负责人：Codex 2026-10-13
里程碑：一期验收
依赖：沿用当前 T-048 分支实现，不修改打印功能
设计书：§demo

## 目标

按用户截图请求，将 WinForms 默认图标换成深蓝底白色条码图标。主窗口、打印窗口、任务栏和 EXE 使用同一图标，在小尺寸和高分屏上保持清晰。

## 范围

- `client/samples/LabelService.Demo.WinForms/Assets/AppIcon.ico`
- `client/samples/LabelService.Demo.WinForms/DemoIcon.cs`
- `client/samples/LabelService.Demo.WinForms/LabelService.Demo.WinForms.csproj`
- `client/samples/LabelService.Demo.WinForms/MainForm.cs`
- `client/samples/LabelService.Demo.WinForms/PrintForm.cs`
- `client/samples/LabelService.Demo.WinForms/MainForm.Smoke.cs`
- `client/build/New-DemoIcon.ps1`
- `client/README.md`
- 本任务文件及任务索引。

## 共享文件

- `docs/tasks/README.md`：追加任务索引，单独提交。

## 验收标准

- [x] 图标包含 16、24、32、48、64、128、256 像素尺寸。
- [x] net48 和 net10.0-windows EXE 图标、主窗口和打印窗口图标一致，无额外图标文件部署要求。
- [x] 两个框架的演示程序 smoke 通过，截图检查图标；客户端已重新打开。
- [x] `verify.ps1` 通过（含文档改动，跑全量）。
- [x] 相关文档已更新。

## 不做

- 不修改打印行为、网页品牌设置、SDK 或接口契约。
- 不新增第三方依赖，不合并主分支或推送远程。

## 交接记录

- 2026-10-13 · Codex · 原验证阻塞由 T-051 修复：系统 PDF 位图按请求 DPI 归一，完整 verify 的后端 520、调用端 136、前端 104 项通过。两个框架 smoke 各 31 项通过，包含图标、布局和模拟重试检查；证据在 artifacts/batch-print。图标产品实现未再改变，当前客户端已随新打印版本恢复，待用户验收。

- 2026-10-13 · Codex · 图标实现完成：脚本自绘七种尺寸 ICO，编译嵌入 EXE 和托管资源，主窗口及打印窗口共用 DemoIcon，README 同步。net48、net10.0-windows 编译均零警告；提取两个 EXE 的图标并逐张查看，net48 窗口截图确认默认图标已替换。完整验证后端 520 项通过，前端 104 项及构建通过；调用端在 PDF 按生成 DPI 渲染且保留准确物理尺寸检查失败，独立重跑仍失败，该打印源码本任务未修改。net48 smoke 的图标和其他流程通过，模拟网络抖动日志期望失败（共 21 项，20 通过）；首次后台自检超过工具等待时间，随后自行退出 1。按 R13 将验收标记阻塞，需单独查明 PDF 尺寸及 smoke 重试日志问题后再验收，本次不修改范围外的打印实现。首轮全量验证因正在运行的后端锁文件失败，停止自己启动的服务后重跑。证据在忽略目录 artifacts/demo-icon；后端和前端服务恢复，普通客户端窗口重新打开，不提交真实打印。

- 2026-10-13 · Codex · 用户已明确要求替换截图中的客户端默认图标。任务分支从 main 创建，再快进到现有 T-048 实现，保留用户当前测试的打印版本，main 未改动。共享文件登记：任务索引仅追加一行。图标借用当前产品条码的识别语义，使用自绘几何图形和系统绘图库，不涉及新的依赖或交互设计。
