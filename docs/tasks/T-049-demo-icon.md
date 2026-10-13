# T-049 客户端程序图标

状态：进行中
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

- [ ] 图标包含 16、24、32、48、64、128、256 像素尺寸。
- [ ] net48 和 net10.0-windows EXE 图标、主窗口和打印窗口图标一致，无额外图标文件部署要求。
- [ ] 两个框架的演示程序 smoke 通过，截图检查图标；客户端已重新打开。
- [ ] `verify.ps1` 通过（含文档改动，跑全量）。
- [ ] 相关文档已更新。

## 不做

- 不修改打印行为、网页品牌设置、SDK 或接口契约。
- 不新增第三方依赖，不合并主分支或推送远程。

## 交接记录

- 2026-10-13 · Codex · 用户已明确要求替换截图中的客户端默认图标。任务分支从 main 创建，再快进到现有 T-048 实现，保留用户当前测试的打印版本，main 未改动。共享文件登记：任务索引仅追加一行。图标借用当前产品条码的识别语义，使用自绘几何图形和系统绘图库，不涉及新的依赖或交互设计。
