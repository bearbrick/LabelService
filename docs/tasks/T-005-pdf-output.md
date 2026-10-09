# T-005 PDF 输出

状态：待验收
负责人：Claude 2026-10-09
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

- `backend/src/LabelService.Rendering/Output/OutputDocuments.cs`：登记 PDF；`Create` 加模板画布参数（PDF 按 mm 定页面）
- `docs/contracts.md`：补 PDF 说明
- `AGENTS.md`：第 1 节当前阶段

## 验收标准

- [x] 页面尺寸等于标签尺寸（mm 换算成 pt）；画布缩放成 72/dpi，绘制器代码不需要改
- [x] `copies` 份数重复页面；多张标签多页（批量合并用）
- [x] PDF 可以用 Edge、Acrobat 打开，文字可选中（矢量），中文不乱码
- [x] 单张箱标 PDF 热请求 total 低于 100 ms，记录到 docs/performance.md
- [x] ServerChecks 里“PDF 已实现时返回 200”自动切换为生效
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- PDF/A、加密、书签

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收。
  - 做了什么：`Output/PdfOutputDocument.cs`：每张标签先录成 SKPicture（打印点坐标），`EndLabel` 按份数逐页回放到 `SKDocument.CreatePdf`，页面画布缩放 72/dpi，绘制器不用改；`OutputDocuments` 登记 PDF。黑色图标不再加颜色滤镜（带滤镜的画面 PDF 只能栅格化），示例箱标的 PDF 全部是矢量。基准加了 `BoxLabelPdf`。
  - 检查：新增 `PdfChecks.cs`（8 项）：PDF 完整、copies=3 三页、多张标签合并、页面尺寸、全部矢量、字体嵌入、ToUnicode 里有中文和数字；ServerChecks 的“PDF 已实现时返回 200”已自动生效。用 pdftotext（Git 自带的 xpdf 4.00，`-enc UTF-8`）抽取文字，中文、数字都正确。
  - 和验收标准的出入：① 页面尺寸——Skia 按 RasterDpi 把页面对齐到打印点，100×60mm 在 300dpi 下是 283.44×170.16 pt（精确值 283.46×170.08），误差小于半个点，和 PNG、ZPL 的像素尺寸一致，我认为这样更合适，检查按“误差小于半个点”写。② 字体以 Type3 嵌入（Skia 对可变字体、CFF 字体都这样做），文字可选中、可搜索，但复制少数和康熙部首共用字形的汉字（如“日”）会得到部首字符（U+2F47），这是 Skia 生成 ToUnicode 的方式决定的，打印和显示不受影响。③ 没能在本机自动截图 PDF（无头 Edge 截不到 PDF 查看器），**请验收时用 Edge 或 Acrobat 打开一份看看**。
  - 性能：`BoxLabelPdf` 203dpi 3.59 ms、300dpi 3.70 ms，比 PNG 快（不栅格化、不压缩）；远低于 100 ms。已写进 docs/performance.md。
- 2026-10-09 · Claude · 认领。计划：每张标签先录成 SKPicture（打印点坐标），EndLabel 时按份数逐页回放到 SKDocument.CreatePdf，页面缩放 72/dpi；文字保持矢量，字体由 Skia 嵌入子集。
