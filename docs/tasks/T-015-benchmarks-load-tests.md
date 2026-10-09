# T-015 基准门禁与压测

状态：待认领
负责人：—
里程碑：M4
依赖：T-001～T-006（基准要覆盖真实元素和格式）
设计书：§9 压测与防退化

## 目标

渲染变慢能在合并前被拦住；有可重复的压测脚本证明单张 P95 ≤ 100 ms。

## 范围

可以新建、修改的文件：

- `backend/tests/LabelService.Benchmarks/`：每个示例模板 × PNG / PDF / ZPL 的基准；A4 唛头模板
- 新建 `backend/tests/load/`：k6 脚本（单张 50 并发、批量 100 张）
- 新建 CI 配置（平台待定）：每次提交跑基准，中位数比基线慢 20% 以上失败
- `backend/samples/templates/`：补 A4 唛头等示例模板

## 共享文件

- `backend/samples/templates/`（新增模板会被检查程序和服务端自动加载）

## 验收标准

- [ ] 基线结果存进仓库（如 `backend/tests/LabelService.Benchmarks/baseline.json`），有更新基线的说明
- [ ] 压测报告模板：机器配置、并发、P50/P95/P99、错误率、吞吐
- [ ] 在 4 核 8 GB 目标机器上：单张 50 并发 P95 ≤ 100 ms、错误率 0、吞吐 ≥ 60 次/秒
- [ ] docs/performance.md 更新基线和压测结果
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 生产环境压测

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
