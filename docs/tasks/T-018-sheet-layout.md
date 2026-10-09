# T-018 A4 拼版：版式与拼版渲染

状态：待认领
负责人：—
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

## 验收标准

- [ ] 版式 JSON 规范写进 `docs/contracts.md`：单位 mm，格子数组顺序就是填充顺序，旋转只允许 0、90、180、270
- [ ] 单张接口 `copies: 8` + `layout: A4_100x60_2x4` 输出 1 页 8 张；批量 19 张 + `startSlot: 3` 输出 3 页（6 + 8 + 5）
- [ ] 格子位置准确：渲染成 PNG 后，标签外框位置和版式坐标的偏差 ≤ 0.1 mm（检查程序逐格验证），旋转 90° 的格子方向正确
- [ ] 打印偏移作用于整页；可选的格子边框（裁切线）能开关
- [ ] 版式不存在返回 404 `LAYOUT_NOT_FOUND`；模板尺寸和格子尺寸不一致返回 400 `LAYOUT_SIZE_MISMATCH`；ZPL 加版式返回 `FORMAT_INVALID`；不传 `layout` 时所有行为和原来完全一致
- [ ] 响应头 `X-Page-Count` 正确；`X-Label-Count` 仍是标签张数；批量上限按标签张数算
- [ ] 不随数据变化的内容每页只计算一次（基准对比有无此优化）
- [ ] 性能：A4 一页 24 张标签的 PDF，基准中位数明显低于 250 ms，记录到 `docs/performance.md`
- [ ] SDK 端到端：`Layout` 参数生成多页 PDF，`PageCount` 正确
- [ ] `verify.ps1` 全量通过（见 AGENTS.md）

## 不做

- 拼版设计器界面（T-019）
- 模板嵌套（T-020，二期）
- ZPL 拼版

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 新建任务：A4 拼版进初版，采用独立的版式（ADR 0005）。
