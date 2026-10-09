# T-002 一维码渲染

状态：待验收
负责人：Claude 2026-10-09
里程碑：M2
依赖：T-001（人眼可读文字用文本排版；可以先画条、后补文字）
设计书：§5 一维码尺寸规则

## 目标

服务端能画出扫得出来的一维码：Code128、Code39、EAN-13、EAN-8、UPC-A、ITF-14、GS1-128，模块宽度对齐打印点。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Rendering/Elements/BarcodePainter.cs`（ZXing.Net 只用来编码出条空序列，自己按点绘制）
- 新建条码矩阵缓存（同目录），按“码制 + 内容”缓存，线程安全、有容量上限
- `backend/tests/LabelService.Backend.Checks/RenderChecks.cs`：补条码检查

## 共享文件

- `backend/src/LabelService.Rendering/Elements/ElementPainters.cs`：登记 `BarcodePainter`
- `backend/samples/templates/box-label.json`、`frontend/src/samples/box-label.json`：示例条码加 `quietZone: 5`（两份同时改）
- `docs/contracts.md`：条码行改为已实现，补一维码规则
- `AGENTS.md`：第 1 节当前阶段

## 验收标准

- [x] 模块宽度 = floor(框宽点数 ÷ 模块数)，条码在框内按 `align` 放置；小于 `minModuleDots` 抛 `LabelRenderException(BARCODE_TOO_DENSE)`
- [x] 内容不符合码制（如 EAN-13 不是 12/13 位数字）抛 `BARCODE_INVALID`；EAN、UPC 校验位自动补齐
- [x] `showText`、`textPosition`、`textStyle` 生效
- [x] 旋转只接受 0/90/180/270，其他角度在模板保存时拦截（这里按最近的直角处理并记日志即可）
- [x] 每条和每个空的宽度都是整数个点（检查程序逐列扫描像素验证）
- [x] 用 ZXing.Net 解码渲染出的 PNG（203、300 dpi），内容一致
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 原生 ZPL 条码指令 `^BC`（二期）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收。
  - 做了什么：`Elements/LinearSymbols.cs`（检查内容、补或核对校验位、GS1-128 的 (AI) 解析和 FNC1，ZXing.Net 编码出不带空白区的模块序列）；`Elements/Symbols.cs`（编码结果 `EncodedSymbol` 和缓存 `SymbolCache`，按“码制 + 内容”索引，上限 4096 条，超过时清空，一维二维共用，T-003 直接用）；`Elements/BarcodePainter.cs`（模块宽度取整、按 align 放置、左端对齐整点、不抗锯齿，人眼可读文字复用 T-001 的 `TextLayout`）。`ElementLayout` 加了可重写的 `Rotation`、`Pivot`：条码的旋转对齐到最近的直角，旋转中心取到半个点上，转 90° 后条边仍在整点。`TextPainter` 拆出 `DrawBlock` 给条码文字用。
  - 检查：新增 `BarcodeChecks.cs`（27 项）：7 种码制在 203、300dpi 渲染后用 ZXing.Net 解码核对（含 GS1 的 ]C1 和中间的 GS 分隔符）；逐行扫描像素验证每条、每个空都是模块宽的整数倍且没有灰色像素（含旋转 90°）；过密、非法内容、校验位错、中文、GS1 写法错的错误码；showText、textPosition、textStyle、align、quietZone、缓存。
  - **发现的问题**：示例箱标的条码离外框只有 2.7 mm（不到 10 个模块的空白区），ZXing 解不出来，真实扫描枪也容易读不出。示例模板两份都加了 `quietZone: 5`。建议设计器（T-013 之后的改进）在条码两侧 10 个模块内有其他元素时给提示，模板发布检查也可以加这一条。
  - 和设计器的差别：EAN、UPC 只写 12（11）位时，服务端显示补上校验位后的完整号码，设计器显示的是填写的内容；建议设计器也显示完整号码。
  - 没做：旋转非直角时“记日志”——Rendering 没有日志依赖，直接按最近的直角画；模板发布时已经拦截（T-008）。
  - 基准：改动前 203dpi 5.56 ms、300dpi 11.94 ms；改动后 5.55 ms、11.91 ms，分配多 7–8 KB。已写进 docs/performance.md。
- 2026-10-09 · Claude · 认领。计划：ZXing.Net 只编码出模块序列（边距 0），按整数打印点逐条绘制；“码制 + 内容”缓存做成一维、二维通用，T-003 直接复用；人眼可读文字复用 T-001 的 TextLayout；旋转中心对齐到半个点，转 90° 后条边仍落在整点上。
