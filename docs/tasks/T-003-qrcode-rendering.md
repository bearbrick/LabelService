# T-003 二维码渲染

状态：待认领
负责人：—
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

## 验收标准

- [ ] 模块大小 = floor(min(框宽, 框高)点数 ÷ 模块数)，居中放置；模块小于 2 点时抛 `BARCODE_TOO_DENSE`
- [ ] `ecLevel` 对 QR 生效；内容可以拼接多个字段，支持中文（UTF-8）
- [ ] `quietZone`（mm）生效
- [ ] 用 ZXing.Net 解码渲染出的 PNG，内容一致
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 彩色二维码、带 Logo 的二维码

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
