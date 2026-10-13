# 0015 批量结果按业务条目或拼版页组织，集合直接提交打印

状态：已采纳（用户 2026-10-13 明确确认并要求实现集合、单项及子集打印）
日期：2026-10-13
关联：T-051、ADR 0014

## 背景

集成方需要生成一次后选择整个集合或其中某项补打，不应自己解压 ZIP、拆分 PDF 或再次生成导致自动字段变化。原 RenderBatchAsync 和 Print(file) 已在使用，需要保持兼容。HTTP SDK 支持 net461 且不能引入第三方依赖。

## 决定

- 新增 RenderBatchFilesAsync/RenderBatchFiles 扩展方法，返回 LabelBatchResult，其 Items 是只读的 LabelBatchItem 集合；保持 ILabelServiceClient 接口不变，已有替代实现者无需修改。
- 新方法复制请求并在普通批量时选择已有 ZIP 输出，一次生成全部结果。PNG 多份根据快照中的条目份数分组，PDF/ZPL 每个条目一个文件，保留响应 DPI、实际版本、份数和字节内容。
- 集合明确使用 Item 或 Page 单位；普通条目带 ItemNumber、Copies 和 Files，拼版项带 PageNumber。PDF 拼版项保存原 PDF 及页引用，Windows 打印时只渲染选中页，避免在跨平台 SDK 引入 PDF 分割器。
- WindowsLabelPrinter 增加单项及集合 Print/CreateDocument 重载；PNG/PDF 集合共用一个文档连续分页，ZPL 集合合并为一个 RAW 作业。混合 ZPL 与图形打印拒绝，提交前核对集合；返回只代表提交结束，不代表所有实物打印成功。
- 原 RenderBatchAsync、Print(LabelFile)、CreateDocument(LabelFile) 保留。拆分或保存独立 PDF 页不是该接口的能力，OriginalFile 保留原服务输出用于留底。
- 演示程序批量代码直接使用集合，并提供注释示例说明整批、单项和指定子集；界面使用独立的“已生成项”选择框，明确区分生成条目与仅用于查看的缩略图。

## 备选方案

- 每项重新请求：自动日期、编号及模板版本可能变化，用户明确不需要。
- 全部强制转成 PNG：丢失 PDF 原始内容且改变输出，不采用。
- 为跨平台 SDK 添加 PDF 拆分库：破坏零依赖要求，不采用。
- 改变旧接口返回类型或向旧接口增加抽象成员：影响已有调用或实现者，使用新增模型及扩展方法。

## 影响

集合保留原服务内容，不写临时文件。原 ZIP 与拆出的文件暂时共同占用内存，批量规模沿用服务上限。PDF 拼版的单项是可打印页引用，Files 内的 PDF 仍为完整原文档；调用方打印该项应传 LabelBatchItem，不能抹去 PageNumber 后直接打印 Files。物理打印的失败和重复补打仍由业务系统确认，不自动重试打印作业。

PDF 系统渲染的 DestinationWidth/Height 使用 DIP，在当前高 DPI 桌面上实际返回位图会额外缩放；Windows 模块将位图归一到请求 DPI 的像素数，物理尺寸仍取 PDF 页尺寸。依据：[Microsoft DestinationWidth 文档](https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfpagerenderoptions.destinationwidth)。
