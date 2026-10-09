# T-005 PDF 输出

状态：待认领
负责人：—
里程碑：M2
依赖：T-001（字体嵌入）
设计书：§7 输出格式

## 目标

`format: pdf` 输出矢量 PDF，一张标签一页，字体嵌入子集，条码仍按 DPI 对齐。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Rendering/Output/PdfOutputDocument.cs`（SkiaSharp `SKDocument.CreatePdf`）
- `backend/tests/LabelService.Backend.Checks/RenderChecks.cs`、`ServerChecks.cs`：补 PDF 检查

## 共享文件

- `backend/src/LabelService.Rendering/Output/OutputDocuments.cs`：登记 PDF

## 验收标准

- [ ] 页面尺寸等于标签尺寸（mm 换算成 pt）；画布缩放成 72/dpi，绘制器代码不需要改
- [ ] `copies` 份数重复页面；多张标签多页（批量合并用）
- [ ] PDF 可以用 Edge、Acrobat 打开，文字可选中（矢量），中文不乱码
- [ ] 单张箱标 PDF 热请求 total 低于 100 ms，记录到 docs/performance.md
- [ ] ServerChecks 里“PDF 已实现时返回 200”自动切换为生效
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- PDF/A、加密、书签

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
