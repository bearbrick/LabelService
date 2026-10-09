# T-006 ZPL 输出与黑白化

状态：待验收
负责人：Claude 2026-10-09
里程碑：M2
依赖：—
设计书：§7 输出格式

## 目标

`format: zpl` 把整张标签栅格化成黑白位图，用 `^GFA` 输出，斑马等标签机可以直接打印；同时实现 PNG 的 `mono`。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Rendering/Output/ZplOutputDocument.cs`
- 新建 `backend/src/LabelService.Rendering/Output/Monochrome.cs`：黑白化（文字、线条用阈值，图片区域用抖动），PNG 和 ZPL 共用
- `backend/src/LabelService.Rendering/Output/PngOutputDocument.cs`：接上 `mono`
- `backend/tests/LabelService.Backend.Checks/RenderChecks.cs`

## 共享文件

- `backend/src/LabelService.Rendering/Output/OutputDocuments.cs`：登记 ZPL
- `docs/contracts.md`：补 ZPL 和 PNG 黑白说明
- `AGENTS.md`：第 1 节当前阶段

## 验收标准

- [x] 每张标签一个 `^XA … ^XZ`，`^PW`、`^LL` 按点数设置，份数用 `^PQ`
- [x] 位图按行打包成十六进制，查表实现，不逐位拼字符串；可选 `:Z64:` 压缩（先测性能再决定）
- [ ] 输出用 ZPL 查看器（如 Labelary）检查一次，和 PNG 一致；截图附在交接记录——没做，见交接记录
- [x] 203dpi 单张箱标 ZPL 热请求 total 低于 100 ms，记录到 docs/performance.md
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 原生 ZPL 文本和条码指令（二期）、TSPL

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收（Labelary 核对一项没做，见下）。
  - 做了什么：`Output/Monochrome.cs`（按 BT.601 亮度阈值 128 归黑白，打包成 1 位，高位在左，PNG 和 ZPL 共用）；`Output/ZplOutputDocument.cs`（每张标签 `^XA^PW^LL^LH0,0 ^FO0,0^GFA … ^FS ^PQ ^XZ`，十六进制查表，ZPL ASCII 压缩：计数字母、`,` 补 0、`!` 补 F、`:` 重复上一行）；PNG 的 mono 接上；ZPL 输出时绘制器按黑白画（文字线条不抗锯齿、图片抖动），再阈值化。基准加了 `BoxLabelZpl`。
  - 没用 `:Z64:`：要算 CRC，各型号要求不一，没有打印机实测前不冒险；ASCII 压缩已经把示例箱标从 96000 个十六进制字符压到 10760 个（约 10.8 KB）。
  - 检查：新增 `ZplChecks.cs`（7 项）：解码器按 ZPL II 手册独立实现，先用手册里的例子（vMB = 327 个 B）核对计数字母，再把示例箱标的 `^GFA` 解码，和黑白 PNG 逐像素比较（0 点不一致）；结构、份数、多张、压缩率、黑白 PNG 只有纯黑纯白。
  - **没做**：用 Labelary 在线查看器核对。Labelary 是外部服务，要把标签数据发出去，需要先征得同意；也可以直接在斑马打印机上试打一张（`backend/samples/requests/box-label.render.json` 把 format 改成 zpl）。已列入待确认问题。
  - 性能：`BoxLabelZpl` 203dpi 1.09 ms、300dpi 2.28 ms，远低于 100 ms。已写进 docs/performance.md。
- 2026-10-09 · Claude · 认领。计划：ZPL 输出时整张按黑白渲染（图片走抖动），画完按亮度阈值转 1 位；^GFA 用 ZPL 自带的 ASCII 压缩（重复计数、逗号补零、冒号重复上一行），不用 :Z64:（要 CRC，没打印机验证）；检查程序写一个按 ZPL 手册规则的解码器做往返核对。
