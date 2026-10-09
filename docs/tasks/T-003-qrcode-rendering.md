# T-003 二维码渲染

状态：待验收
负责人：Claude 2026-10-09
里程碑：M2
依赖：—
设计书：§4 元素类型、§5 元素属性

## 目标

服务端能画出 QR、DataMatrix、PDF417，模块对齐打印点，扫码可读。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Rendering/Elements/QrCodePainter.cs`
- 可与 T-002 共用矩阵缓存（先协调，谁先做谁建）
- `backend/tests/LabelService.Backend.Checks/RenderChecks.cs`：补二维码检查

## 共享文件

- `backend/src/LabelService.Rendering/Elements/ElementPainters.cs`：登记 `QrCodePainter`
- `docs/contracts.md`：qrcode 行改为已实现，补二维码规则
- `AGENTS.md`：第 1 节当前阶段

## 验收标准

- [x] 模块大小 = floor(min(框宽, 框高)点数 ÷ 模块数)，居中放置；模块小于 2 点时抛 `BARCODE_TOO_DENSE`
- [x] `ecLevel` 对 QR 生效；内容可以拼接多个字段，支持中文（UTF-8）
- [x] `quietZone`（mm）生效
- [x] 用 ZXing.Net 解码渲染出的 PNG，内容一致
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 彩色二维码、带 Logo 的二维码

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收。
  - 做了什么：`Elements/MatrixSymbols.cs`（QR、DataMatrix、PDF417 编码，复用 T-002 的 `SymbolCache`；含非 ASCII 时用 UTF-8 并带 ECI，纯 ASCII 不加；DataMatrix 强制正方形；PDF417 行高 3 个模块，和设计器 bwip-js 的默认一致）；`Elements/QrCodePainter.cs`（模块取整、居中、不抗锯齿，转直角时旋转中心对齐半点）。旋转中心的计算挪到 `SymbolGeometry.Pivot`，一维码、二维码共用。
  - 检查：新增 `QrCodeChecks.cs`（14 项）：示例箱标二维码 203、300dpi 解码；三种码制各一组 ASCII 和中文内容在两种 DPI 下解码；ecLevel、PDF417 行高、DataMatrix 正方形；模块大小和居中、无灰色像素、quietZone、旋转 90° 解码；过密、超容量、空内容的错误码。
  - 注意：示例箱标的二维码下方 1.4 mm 处是图标（T-004 实现后才会画出来），不到 QR 规定的 4 个模块空白区。T-004 做完后如果示例二维码解码失败，要调整示例模板里图标或二维码的位置。
  - 基准：改动前 203dpi 5.55 ms、300dpi 11.91 ms；改动后 6.14 ms、12.49 ms。已写进 docs/performance.md。
- 2026-10-09 · Claude · 认领。计划：复用 T-002 的 SymbolCache；ZXing.Net 编码（边距 0），内容含中文时按 UTF-8 编码并带 ECI；DataMatrix 固定正方形，PDF417 行高取 3 个模块（和设计器的 bwip-js 一致）；模块取整、居中、不抗锯齿。
