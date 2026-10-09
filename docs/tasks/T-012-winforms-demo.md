# T-012 WinForms 演示程序完整版

状态：待认领
负责人：—
里程碑：M2
依赖：T-011（SDK 公开 API 稳定）
设计书：§11 调用演示程序；原型 docs/prototypes/label-sdk-demo-prototype.html

## 目标

按原型实现 LabelServiceDemo：连接设置、按字段生成录入表格、单张和批量、预览、发送到打印机、调用代码、调用日志、内置模拟服务。

## 范围

可以新建、修改的文件：

- `client/samples/LabelService.Demo.WinForms/` 下全部文件（现在只有最小窗体 `MainForm.cs`，可以整体重写）
- 新建 `client/samples/LabelService.Demo.WinForms/Mock/MockLabelServiceHandler.cs`：不连服务器也能演示，可模拟 503 抖动

## 共享文件

- 无

## 验收标准

- [ ] 设计书第 11 节的 5 条验收标准全部满足
- [ ] net48 和 net10.0-windows 两个版本都能运行，界面一致
- [ ] 调用日志显示总耗时、服务端耗时、重试次数、requestId、traceId；单张超过 100 ms 标为慢调用
- [ ] “调用代码”页签生成的代码在新建的 .NET Framework 4.8 和 .NET 10 项目里能编译
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- PDF 本地渲染打印（交给系统默认程序）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
