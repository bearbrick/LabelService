# T-018 A4 拼版：版式与拼版渲染

状态：待验收
负责人：Claude 2026-10-10
里程碑：M3
依赖：T-005（PDF 输出）；版式存数据库依赖 T-008，之前先用文件
设计书：§3 数据模型、§7 拼版渲染、§8 拼版打印、§9 性能；ADR 0005

## 目标

同一个模板既能单张打，也能按“版式”拼在 A4 等纸张上打：调用方传 `layout` 参数，服务按格子顺序把多张标签排到每一页，输出多页 PDF 或 PNG。

## 范围

可以新建、修改的文件：

- `backend/src/LabelService.Contracts/`：版式模型（纸张、格子尺寸、格子位置和旋转、打印偏移）、请求参数 `layout`、`startSlot`，响应头 `X-Page-Count`，错误码 `LAYOUT_NOT_FOUND`、`LAYOUT_SIZE_MISMATCH`
- `backend/src/LabelService.Rendering/`：按版式排页的渲染流程；输出文档支持“一页多张标签”（PDF 一页一张纸，PNG 一页一张图）
- `backend/src/LabelService.Server/`：版式读取（T-008 完成前用 `backend/samples/layouts/*.json` 文件加载，完成后存 `sheet_layout` 表）、单张和批量接口的参数处理、调用日志记录版式编码
- `backend/samples/layouts/`：示例版式，至少一个和示例模板（100×60mm）匹配的 A4 版式，如 `A4_100x60_2x4`
- `client/src/LabelService.Client/`：`RenderRequest`、`BatchRenderRequest` 增加 `Layout`、`StartSlot`，`LabelFile` 增加 `PageCount`，同步错误码和 HTTP 头副本
- 后端和调用端的检查程序：补拼版检查

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `backend/src/LabelService.Contracts/**`、`client/src/LabelService.Client/LabelErrorCodes.cs`、`client/src/LabelService.Client/Internal/Protocol.cs`（契约变更，按 AGENTS.md §6.4 同一个提交改齐）
- `backend/src/LabelService.Rendering/Output/OutputDocuments.cs`
- `backend/src/LabelService.Server/Endpoints/PublicApiEndpoints.cs`
- `docs/contracts.md`：写入版式 JSON 规范、新参数、新响应头、新错误码
- 实际还改了（都单独提交）：`docs/design/label-service-design.html`（同步：sheet_layout 加 cut_lines 列、START_SLOT_INVALID、拼版时 output 不起作用）、`AGENTS.md` 第 1 节、`client/Directory.Build.props`（SDK 1.0.0-preview.2）、`backend/tests/LabelService.Benchmarks/baseline.json`（新增两项）。没有改 `OutputDocuments.cs`

## 验收标准

- [x] 版式 JSON 规范写进 `docs/contracts.md`：单位 mm，格子数组顺序就是填充顺序，旋转只允许 0、90、180、270
- [x] 单张接口 `copies: 8` + `layout: A4_100x60_2x4` 输出 1 页 8 张；批量 19 张 + `startSlot: 3` 输出 3 页（6 + 8 + 5）
- [x] 格子位置准确：渲染成 PNG 后，标签外框位置和版式坐标的偏差 ≤ 0.1 mm（检查程序逐格验证），旋转 90° 的格子方向正确
- [x] 打印偏移作用于整页；可选的格子边框（裁切线）能开关
- [x] 版式不存在返回 404 `LAYOUT_NOT_FOUND`；模板尺寸和格子尺寸不一致返回 400 `LAYOUT_SIZE_MISMATCH`；ZPL 加版式返回 `FORMAT_INVALID`；不传 `layout` 时所有行为和原来完全一致
- [x] 响应头 `X-Page-Count` 正确；`X-Label-Count` 仍是标签张数；批量上限按标签张数算
- [x] 不随数据变化的内容每页只计算一次（基准对比有无此优化）
- [x] 性能：A4 一页 24 张标签的 PDF，基准中位数明显低于 250 ms，记录到 `docs/performance.md`
- [x] SDK 端到端：`Layout` 参数生成多页 PDF，`PageCount` 正确
- [x] `verify.ps1` 全量通过（见 AGENTS.md）

## 不做

- 拼版设计器界面（T-019）
- 模板嵌套（T-020，二期）
- ZPL 拼版

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

### 2026-10-10 · Claude

**做了什么**

- 契约（一个提交改齐）：`Contracts/Layouts/SheetLayout.cs`（纸张、格子尺寸、格子 x/y/rotation、打印偏移、`cutLines`），请求参数 `layout`、`startSlot`，响应头 `X-Page-Count`，错误码 `LAYOUT_NOT_FOUND`（404）、`LAYOUT_SIZE_MISMATCH`（400）、`START_SLOT_INVALID`（400），上限 `MaxLayoutSlots` 500、纸张 1000 mm；SDK 副本、`docs/contracts.md`“拼版”一节、设计书同步。
- 渲染：`Rendering/Sheets/SheetRenderer.cs` 按格子顺序放标签（平移到整数打印点、旋转、裁到标签大小），一页排满换页；PDF 一页一张纸，PNG 一页一张图、多页打包 ZIP（`RenderResult.IsZip`、`PageCount`）；整页打印偏移；`cutLines` 给放了标签的格子画 0.1 mm 灰框。`LabelPlan` 把不引用字段的元素按叠放顺序分段，排版一次录成 SKPicture 各格子回放。不传版式时单张流程不变。
- 服务端：表 `sheet_layout`（迁移 AddSheetLayout）+ 内存快照 `Layouts/LayoutStore.cs`；开发环境版式表为空时导入 `backend/samples/layouts/A4_100x60_2x4.json`，模板来源 Files 时也读这个目录；单张、批量接口的参数检查在 `Endpoints/SheetParameters.cs`；调用日志写 `layout_code`（T-009 已留好列）。
- 管理接口 `/api/admin/layouts`（`docs/admin-api.md`）：列表（可按格子尺寸筛选）、详情、新建、修改/停用、删除（res.manage），预览还没保存的版式（tpl.view，不给数据时用样例值铺满一页），打印校准测试页（每格边框、中心十字、序号，用临时模板走同一套拼版渲染）。这些是给 T-019 拼版设计器准备的，任务范围里没写明，我按“版式存数据库”顺带做了。
- SDK 1.0.0-preview.2：`Layout`、`StartSlot`、`LabelFile.PageCount`，本地预校验跟着放宽（版式时 PNG 可多份、批量不要求 Zip）；README、CHANGELOG。

**验证**

- 后端检查 397 项（新增 `LayoutRenderChecks`、`LayoutServerChecks`）：逐格测外框四条边，最大偏差 0.048 mm（含 90°、180°、270° 的格子）；标记块验证旋转方向；分页、startSlot、偏移、裁切线开关；固定元素回放和逐格重排的 PNG 字节完全一样；错误码；版式检查（重叠、越界、旋转角度、贴边容差）；管理接口全流程。
- 调用端检查 41 项，含对真实服务的拼版端到端；完整 `verify.ps1` 通过。
- 基准（`SheetBenchmarks`，A4 一页 24 张 70×37，300 dpi）：PDF 中位数 7.3 ms（预算 250 ms）；PNG 122 ms，基本是 A4 整页 PNG 编码。固定元素只排版一次：PDF 时间 7.31 对 7.31 ms，看不出差别（文本测量、条码编码本来有缓存），分配少 19%。数字写进了 `docs/performance.md`。

**坑**

- 第一次跑完整基准门禁，排在拼版 PNG 后面的 TemplateBenchmarks 误报慢 30%（和它同路径的 RenderBenchmarks 没变），单独重跑都在 ±3% 内，判断是笔记本连续高负载降频。所以门禁里只放拼版 PDF 两项。
- 逐格测边时相邻格子贴边会互相干扰，检查用的版式留了 2 mm 间隔、搜索窗口 15 个点。

**问题（等人确认）**

- 新增错误码 `START_SLOT_INVALID`：设计书只写了两个拼版错误码，startSlot 超过格子数没说返回什么；按向后兼容的方式加了一个，设计书第 8 节已同步。
- “可选的格子边框”我放在版式里（`cutLines`，表里加 `cut_lines` 列），理由是预切的不干胶纸和白纸是两种版式；如果希望调用方每次请求自己选，可以再加一个请求参数。
- 演示程序（T-012）没有加拼版选项，设计书第 11 节没要求。

**留给下一位（T-019）**

- 版式 JSON 的 TypeScript 镜像按 `docs/contracts.md`“拼版”写；保存走 `PUT /api/admin/layouts/{code}`，预览和测试页接口见 `docs/admin-api.md`。格子坐标是“所占矩形的左上角”，旋转 90°/270° 时宽高互换。
- 2026-10-09 · Claude · 新建任务：A4 拼版进初版，采用独立的版式（ADR 0005）。
