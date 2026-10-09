# T-007 批量生成接口

状态：待认领
负责人：—
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

- `backend/src/LabelService.Server/Endpoints/PublicApiEndpoints.cs`：去掉批量的 501 占位，改为注册新端点

## 验收标准

- [ ] 先校验全部条目，任一条不通过整批返回，错误明细带 `index`
- [ ] 总张数（含份数）超过 500 返回 413 `BATCH_TOO_LARGE`；`output` 非法或 PNG 用 merge 返回 `OUTPUT_INVALID`
- [ ] 输出顺序与 `items` 一致；zip 内文件名 `模板编码_序号.扩展名`
- [ ] PNG / zip 时多张并行渲染，PDF 按页顺序写入
- [ ] Server-Timing 各阶段为全部张数的累计值；X-Label-Count 为总张数
- [ ] 批量 100 张合并 PDF ≤ 3 s（本机），记录到 docs/performance.md
- [ ] SDK 的 `RenderBatchAsync` 对真实服务端调用通过（ServerChecks 端到端）
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 异步批量任务（二期）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
