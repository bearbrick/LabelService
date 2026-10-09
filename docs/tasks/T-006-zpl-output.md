# T-006 ZPL 输出与黑白化

状态：待认领
负责人：—
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

## 验收标准

- [ ] 每张标签一个 `^XA … ^XZ`，`^PW`、`^LL` 按点数设置，份数用 `^PQ`
- [ ] 位图按行打包成十六进制，查表实现，不逐位拼字符串；可选 `:Z64:` 压缩（先测性能再决定）
- [ ] 输出用 ZPL 查看器（如 Labelary）检查一次，和 PNG 一致；截图附在交接记录
- [ ] 203dpi 单张箱标 ZPL 热请求 total 低于 100 ms，记录到 docs/performance.md
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 原生 ZPL 文本和条码指令（二期）、TSPL

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
