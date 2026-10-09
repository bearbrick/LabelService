# T-007 批量生成接口

状态：待验收
负责人：Claude 2026-10-09
里程碑：M2
依赖：T-005、T-006（merge 需要多页 PDF 和拼接 ZPL；zip 只依赖 PNG，可以先做）
设计书：§8 批量生成

## 目标

`POST /api/v1/render/batch` 可用：公共字段 + 条目，`merge` 合并成一个文件，`zip` 每张一个文件打包。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Server/Endpoints/BatchRenderEndpoint.cs`（或在 `PublicApiEndpoints.cs` 旁新建文件，保持单文件小于 500 行）
- 可以把 `PublicApiEndpoints.cs` 里的请求读取、参数检查抽成共用方法
- `backend/tests/LabelService.Backend.Checks/ServerChecks.cs`：补批量检查

## 共享文件

- `backend/src/LabelService.Server/Endpoints/PublicApiEndpoints.cs`：去掉批量的 501 占位，改为注册新端点；`ReadJsonAsync` 改成 internal 共用
- `backend/src/LabelService.Rendering/LabelRenderException.cs`、`LabelRenderer.cs`：渲染错误带批量条目序号
- `client/tests/LabelService.Client.Checks/LiveServerChecks.cs`：SDK 批量端到端检查
- `docs/contracts.md`：批量输出规则，接口状态改为可用
- `AGENTS.md`：第 1 节当前阶段

## 验收标准

- [x] 先校验全部条目，任一条不通过整批返回，错误明细带 `index`
- [x] 总张数（含份数）超过 500 返回 413 `BATCH_TOO_LARGE`；`output` 非法或 PNG 用 merge 返回 `OUTPUT_INVALID`
- [x] 输出顺序与 `items` 一致；zip 内文件名 `模板编码_序号.扩展名`
- [x] PNG / zip 时多张并行渲染，PDF 按页顺序写入
- [x] Server-Timing 各阶段为全部张数的累计值；X-Label-Count 为总张数
- [x] 批量 100 张合并 PDF ≤ 3 s（本机），记录到 docs/performance.md
- [x] SDK 的 `RenderBatchAsync` 对真实服务端调用通过（ServerChecks 端到端）
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 异步批量任务（二期）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收。
  - 做了什么：`Endpoints/BatchRenderEndpoint.cs`：参数检查（output、PNG 只能 zip、每条 copies、总张数 500）→ 取模板 → 全部条目校验（错误带 index）→ merge 用一个 RenderJob 装全部条目（PDF 多页、ZPL 多段，顺序和 items 一致）；zip 用 `Parallel.For` 按核数并行渲染，各条的计时累加到请求计时，按顺序打包（PNG 不再压缩，其他用 Fastest）。`RenderCall` 加 `SucceedFile` 给 zip 用。`LabelRenderException` 加 `LabelIndex` 和 `ForLabel(index)`，合并渲染时出错会标出是第几条；批量只有一条时也补上序号 0。
  - 约定（已写进 docs/contracts.md）：zip 里 PDF、ZPL 一条一个文件、份数在文件里；PNG 一份一个文件；文件名 `模板编码_序号.扩展名`，序号从 001 开始，按文件顺序编号。
  - 检查：新增 `BatchChecks.cs`（16 项，进程内真实服务）：merge PDF 页数、张数头、阶段头、文件名；ZPL 合并和份数；zip 文件名、顺序（解码每个 PNG 的条码核对）、PDF zip 每条一个文件；全部条目校验、merge 和 zip 的渲染错误带序号、BATCH_TOO_LARGE、OUTPUT_INVALID 两种、COPIES_INVALID 带序号、空 items；100 张合并 PDF 计时。调用端 `LiveServerChecks` 加了 SDK 批量合并 PDF、批量 PNG zip 两项端到端检查。
  - 性能：100 张合并 PDF 服务端约 50 ms（要求 ≤ 3 s）。已写进 docs/performance.md。
  - 留给下一位：T-009 写调用日志时，批量的张数用总张数；T-018 拼版要在 merge 的基础上把多张排进一页。
- 2026-10-09 · Claude · 认领。计划：merge 用一个 RenderJob 装全部条目（PDF 多页、ZPL 拼接）；zip 并行渲染后按顺序打包，PDF、ZPL 一条一个文件（份数在文件里），PNG 一份一个文件；渲染错误带条目序号；SDK 的批量端到端检查加在调用端检查程序里。
