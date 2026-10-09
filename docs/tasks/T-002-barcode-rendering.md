# T-002 一维码渲染

状态：待认领
负责人：—
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

## 验收标准

- [ ] 模块宽度 = floor(框宽点数 ÷ 模块数)，条码在框内按 `align` 放置；小于 `minModuleDots` 抛 `LabelRenderException(BARCODE_TOO_DENSE)`
- [ ] 内容不符合码制（如 EAN-13 不是 12/13 位数字）抛 `BARCODE_INVALID`；EAN、UPC 校验位自动补齐
- [ ] `showText`、`textPosition`、`textStyle` 生效
- [ ] 旋转只接受 0/90/180/270，其他角度在模板保存时拦截（这里按最近的直角处理并记日志即可）
- [ ] 每条和每个空的宽度都是整数个点（检查程序逐列扫描像素验证）
- [ ] 用 ZXing.Net 解码渲染出的 PNG（203、300 dpi），内容一致
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 原生 ZPL 条码指令 `^BC`（二期）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
