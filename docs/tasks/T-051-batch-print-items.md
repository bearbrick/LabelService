# T-051 批量结果集合及按项静默打印

状态：待验收
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
- `client/build/pack.ps1`：仅同步发布包内的系统压缩程序集依赖说明。
- `docs/adr/0015-batch-print-items.md`、ADR 索引、任务索引、本任务文件。
- `docs/tasks/T-049-demo-icon.md`、`T-050-print-combo-layout.md`：仅根据本次验证证据关闭原 PDF 检查造成的阻塞，不修改旧任务产品范围。

## 共享文件

- `client/Directory.Build.props`：新公开功能递增预览版本，单独提交。
- `docs/tasks/README.md`、`docs/adr/README.md`：追加索引，单独提交。
- 本次不改 HTTP、模板 JSON 或后端契约，不需要跨部分契约升级。

## 验收标准

- [x] 一次 HTTP 批量生成，自动分组并保留内容、DPI、版本、条目份数及顺序；不修改原请求或接口实现者。
- [x] PNG、PDF、ZPL 普通标签支持集合/单项/子集；PNG 多份保留在业务条目内。
- [x] PNG 和 PDF 拼版支持按纸页选择，PDF 不重新生成或依赖阅读器、第三方库。
- [x] 图形集合通过一个 PrintDocument 连续分页，ZPL 集合通过一个 RAW 作业提交；无效集合在提交前拦下。
- [x] net461/netstandard2.0/net8.0 兼容、零 NuGet 依赖；演示代码简洁、六组生成代码可编译。
- [x] 原 PDF 尺寸检查失败已定位，两个运行时及高 DPI 桌面检查通过；`verify.ps1` 全量通过。
- [x] 公开 API、版本、README、设计书及交接记录同步，无真实打印。

## 不做

- 不改变系统打印机配置，不提交真实打印、不修改服务端渲染热路径。
- 不新增用户维护标准图标、自动值配置或预热增强等无关功能。
- 不合并主分支或推送远程。

## 交接记录

- 2026-10-13 · Codex · 实现完成，待用户实机验收。新增 RenderBatchFilesAsync/RenderBatchFiles 扩展、LabelBatchResult/Item/Unit，原接口成员和单文件入口保留。SDK 自动以内存 ZIP 分组，PNG 份数归属于业务条目，PDF/ZPL 原文件内部保留份数，拼版 PDF 以原文档页引用补打；请求快照保留份数并复制图片字节，不修改原请求。解压核对数量、格式、重复名称和实际长度，限制 256 MiB。Windows 集合、单项、子集重载分别使用一个图形文档或一个 RAW 作业，图形内容、页号及元数据在提交前核对。演示程序批量使用新接口，已生成项与缩略图相互同步，错误及单张清除旧集合；选中未加载预览的项时明确提示，打印不受预览上限限制。PDF 原检查实测请求 203 dpi、2×1 英寸却返回 568×284 像素；系统参数采用 DIP，已归一到 406×203，实际纸页尺寸仍取 PDF。全量 verify：后端 520、调用端 136（含真实服务）、前端 104 项及构建通过。新增栏布局微调后再次 client verify 136 项通过；两个框架 smoke 各 31 项通过（含可见/隐藏下拉框、选择同步、PDF 单项创建），Framework 64/32 位打印检查各 39 项通过，六组生成代码在 net48/C#7.3 和 net10.0-windows 共 12 次编译通过。第一次全量检查被未登记 Git 索引的新源码拦下，登记后通过；首次选择栏启用折行未覆盖原生下拉框高度修正，已改为紧凑独立单行并增加显示后回归。preview.4 两个 nupkg 和 DLL/文档/演示源码 ZIP 已验证打包。证据在忽略目录 artifacts/batch-print；未改后端热路径，未实际提交打印，驱动与硬件出纸仍待用户测试。设计书 §sdk/§demo、架构、三份 README 与版本日志已同步；原任务 T-049/T-050 的 PDF 验证阻塞由本次修复解除。前后端沿用当前数据目录及 Logo，客户端结束时重新启动。

- 2026-10-13 · Codex · 用户明确确认集合及单项打印方案，ADR 0015 记录已授权的实现设计，设计书仅同步新增接口。任务从 main 建立后快进到 T-050 当前版本。使用系统 System.IO.Compression 程序集支持 ZIP，非第三方包；不修改 ILabelServiceClient，而使用扩展方法兼容现有接口实现者。PDF 拼版项是原文档的页引用，普通 PNG 多份按请求顺序归组。共享文件如上登记。当前后台已运行，验证前将关闭自己启动的客户端及后端，结束时沿用当前品牌数据目录恢复。
