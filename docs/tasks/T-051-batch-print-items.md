# T-051 批量结果集合及按项静默打印

状态：进行中
负责人：Codex 2026-10-13
里程碑：一期验收
依赖：沿用当前 T-048～T-050 已实现的打印及显示功能
设计书：§sdk、§demo

## 目标

按用户确认的方案，一次生成后由 DLL 自动返回可打印集合，支持整批、单项和指定若干项打印，无需解压或重新生成。普通标签按业务条目组织、份数归属于条目；拼版按纸页组织，PDF 页项保留原文件及页号引用，不引入 PDF 分割库。

## 范围

- `client/src/LabelService.Client/`：新增批量集合模型、扩展方法、内存 ZIP 读取；项目文件仅加 net461 的系统压缩程序集引用；README、CHANGELOG。
- `client/src/LabelService.Client.Windows/`：集合打印重载及连续分页、PDF 单页引用；定位并修正原 PDF 尺寸检查失败，确保新功能物理尺寸正确；README。
- `client/tests/LabelService.Client.Checks/`：新增集合、条目、份数、顺序、页选择、错误、兼容及端到端检查，Program 登记。
- `client/samples/LabelService.Demo.WinForms/`：调用代码、批量调用结果、打印选择及对应自检，按职责拆文件。
- `client/README.md`、`client/Directory.Build.props`、`docs/architecture.md`、设计书 §sdk/§demo 同步。
- `docs/adr/0015-batch-print-items.md`、ADR 索引、任务索引、本任务文件。

## 共享文件

- `client/Directory.Build.props`：新公开功能递增预览版本，单独提交。
- `docs/tasks/README.md`、`docs/adr/README.md`：追加索引，单独提交。
- 本次不改 HTTP、模板 JSON 或后端契约，不需要跨部分契约升级。

## 验收标准

- [ ] 一次 HTTP 批量生成，自动分组并保留内容、DPI、版本、条目份数及顺序；不修改原请求或接口实现者。
- [ ] PNG、PDF、ZPL 普通标签支持集合/单项/子集；PNG 多份保留在业务条目内。
- [ ] PNG 和 PDF 拼版支持按纸页选择，PDF 不重新生成或依赖阅读器、第三方库。
- [ ] 图形集合通过一个 PrintDocument 连续分页，ZPL 集合通过一个 RAW 作业提交；无效集合在提交前拦下。
- [ ] net461/netstandard2.0/net8.0 兼容、零 NuGet 依赖；演示代码简洁、六组生成代码可编译。
- [ ] 原 PDF 尺寸检查失败已定位，两个运行时及高 DPI 桌面检查通过；`verify.ps1` 全量通过。
- [ ] 公开 API、版本、README、设计书及交接记录同步，无真实打印。

## 不做

- 不改变系统打印机配置，不提交真实打印、不修改服务端渲染热路径。
- 不新增用户维护标准图标、自动值配置或预热增强等无关功能。
- 不合并主分支或推送远程。

## 交接记录

- 2026-10-13 · Codex · 用户明确确认集合及单项打印方案，ADR 0015 记录已授权的实现设计，设计书仅同步新增接口。任务从 main 建立后快进到 T-050 当前版本。使用系统 System.IO.Compression 程序集支持 ZIP，非第三方包；不修改 ILabelServiceClient，而使用扩展方法兼容现有接口实现者。PDF 拼版项是原文档的页引用，普通 PNG 多份按请求顺序归组。共享文件如上登记。当前后台已运行，验证前将关闭自己启动的客户端及后端，结束时沿用当前品牌数据目录恢复。
