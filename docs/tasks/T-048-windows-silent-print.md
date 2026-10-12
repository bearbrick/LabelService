# T-048 Windows 客户端统一静默打印

状态：进行中
负责人：Codex 2026-10-12
里程碑：一期验收
依赖：T-047 已合入 main；用户授权接续实现
设计书：§sdk、§demo

## 目标

实施人员已配置打印机，客户通过一个入口静默打印 PNG、PDF、ZPL。PDF 不依赖阅读器，PNG 和 PDF 沿用 Windows 驱动配置，特殊情况可在打印前调整 PrintDocument 参数。用户已要求正式替换原打印示例。

## 范围

- client/src/LabelService.Client.Windows/**：独立的 Windows 打印模块，net461、net8.0-windows，不改变 HTTP SDK 的目标和依赖。
- client/samples/LabelService.Demo.WinForms/：打印对话框、调用代码生成、提示和自检。
- client/tests/LabelService.Client.Checks/**：新打印行为、PDF 渲染、分页、资源释放、注释和依赖检查。
- client/build/pack.ps1、client/README.md、新模块 README、docs/architecture.md、设计书 §sdk、§demo：实现同步与交付说明。
- docs/adr/0014-windows-silent-print.md、本任务：实现选择和交接。

## 共享文件

- client/LabelService.Client.slnx：增加独立 Windows 打印模块。
- docs/tasks/README.md、docs/adr/README.md：追加索引。

## 验收标准

- [ ] PNG、PDF、ZPL 共用 Print 入口，指定名称或使用默认打印机，默认静默。
- [ ] PNG、PDF 支持 ZIP 和多页，按实际尺寸打印，一个文档连续分页，保留驱动纸张和方向。
- [ ] PDF 通过 Windows 自带接口加载和渲染，不安装阅读器，不新增第三方依赖；旧系统限制明确。
- [ ] 特殊情况通过 CreateDocument 返回的标准 PrintDocument 调整参数；ZPL 保持 RAW，兼容网络发送辅助方法。
- [ ] 演示程序和调用代码使用统一入口，六份生成代码能在目标框架编译。
- [ ] 新检查验证真实 PDF 渲染、页序、尺寸、错误及资源清理；不发送真实打印任务。
- [ ] 演示程序两框架自检通过，verify.ps1 全量通过。
- [ ] 文档、任务交接和测试限制已同步。

## 不做

- 自动配置驱动、切纸控制、真实打印机验收、合并到 main、推送远程。
- HTTP 契约和服务端渲染改动；不引入 PDFium、阅读器或新的 NuGet 包。

## 交接记录

- 2026-10-12 · Codex · 用户关闭 Claude 并授权替换。固定 main e076528，工作区干净，创建独立任务分支。共享文件改动已登记。PDF 使用 Windows.Data.Pdf 系统接口（Windows 8.1+），通过原生 ABI 同时兼容 .NET Framework 和现代 .NET；不改变原 HTTP SDK。该选择记录 ADR，先用真实 PDF 验证，再接入。
