# T-048 Windows 客户端统一静默打印

状态：待验收
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

- [x] PNG、PDF、ZPL 共用 Print 入口，指定名称或使用默认打印机，默认静默。
- [x] PNG、PDF 支持 ZIP 和多页，按实际尺寸打印，一个文档连续分页，保留驱动纸张和方向。
- [x] PDF 通过 Windows 自带接口加载和渲染，不安装阅读器，不新增第三方依赖；旧系统限制明确。
- [x] 特殊情况通过 CreateDocument 返回的标准 PrintDocument 调整参数；ZPL 保持 RAW，兼容网络发送辅助方法。
- [x] 演示程序和调用代码使用统一入口，六份生成代码能在目标框架编译。
- [x] 新检查验证真实 PDF 渲染、页序、尺寸、错误及资源清理；不发送真实打印任务。
- [x] 演示程序两框架自检通过，verify.ps1 全量通过。
- [x] 文档、任务交接和测试限制已同步。

## 不做

- 自动配置驱动、切纸控制、真实打印机验收、合并到 main、推送远程。
- HTTP 契约和服务端渲染改动；不引入 PDFium、阅读器或新的 NuGet 包。

## 交接记录

- 2026-10-12 · Codex · 完成，待用户实机验收。
  - 新增可选 Windows 扩展，原 HTTP SDK 无改动、仍零 NuGet 依赖。三种格式及对应 ZIP 共用 Print；PNG/PDF 默认保留驱动纸张、方向、份数，按物理尺寸分页；特殊参数用 CreateDocument。PDF 不需要阅读器，使用 Windows 8.1+ 系统引擎，通过专用 MTA 线程加载一次、逐页渲染并释放资源。ZPL 设备须支持 ZPL，份数和 DPI 在请求设置；网络辅助方法保留。
  - 演示程序“静默打印”和 Ctrl+P 直接向默认打印机提交完整结果；“工具 → 选择其他打印机”用于特殊情况。后台提交防止界面卡顿和重复点击，关闭窗口后的回调不再操作控件。调用代码只保留一行统一打印，特殊参数和网络 ZPL 写成注释，不再生成阅读器、临时 PDF 或大段格式分派。
  - 验证：最后一次 verify.ps1 全量通过，后端 520、调用端 96（包括服务生成的中文字体/条码 PDF 和 A4 拼版 PDF 的真实系统渲染）、前端 104。新桌面检查 27 项，在现代 .NET、.NET Framework 的 AnyCPU 和 x86 探针均通过；net461 DLL 编译通过，Framework 探针实际运行时是本机 .NET Framework 4.8，不声称只安装 4.6.1 的设备已验证。两套演示程序自检各 20 项通过；六份调用代码分别以 C# 7.3 在 net48、net10.0-windows 编译，12 份构建零警告。
  - 打包成功：HTTP SDK 原 nupkg、可选 Windows nupkg/snupkg、包含两套扩展 DLL 的 zip。包检查使用 NuGet 规范化的 net8.0-windows7.0 路径。脚本同时核对输出清理路径和释放包检查流。没有真实签名密钥和证书，没有验证签名分发。
  - 证据在忽略目录 artifacts/：verify-silent-print-final.txt、windows-print-final-checks.txt、print-framework-probe-result.txt、print-framework-probe-x86-result.txt、silent-print-smoke/、generated-print-build/、pack-silent-print.txt、client-silent-print/。只启动自检和 verify 的临时服务，均已退出；5089 无监听。没有向真实打印队列发送任务，出纸、纸张、切纸和扫码效果由用户明天验收。
  - 文档只同步实现：client README、模块 README、架构的调用端目录、设计书 §sdk 公共 API/扩展说明和 §demo 功能表；ADR 0014 记录系统 PDF 引擎和独立模块。未改 Contracts、Rendering、前端或运行数据，不需要渲染基准。
  - 流程说明：登记共享解决方案的 1d82700 单独检出缺少下一提交中的新项目；完整任务 HEAD 已编译和全量验证，不改写已提交历史。首次全量检查因新源码尚未加入 Git 索引失败，登记后重跑通过；首次包核对因 NuGet Windows 目标规范化路径失败，修正后重跑通过。保留失败记录，不把失败算作成功。
- 2026-10-12 · Codex · 用户关闭 Claude 并授权替换。固定 main e076528，工作区干净，创建独立任务分支。共享文件改动已登记。PDF 使用 Windows.Data.Pdf 系统接口（Windows 8.1+），通过原生 ABI 同时兼容 .NET Framework 和现代 .NET；不改变原 HTTP SDK。该选择记录 ADR，先用真实 PDF 验证，再接入。
