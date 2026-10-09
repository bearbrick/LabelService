# T-010 监控接入

状态：待认领
负责人：—
里程碑：M4
依赖：—（监控平台待确认：公司现有平台还是自建 Prometheus + Grafana）
设计书：§9 监控、告警

## 目标

指标和链路可以被 Prometheus 抓取，有 Grafana 仪表盘和告警规则样例。

## 范围

可以新建、修改的文件：

- `backend/src/LabelService.Server/Diagnostics/`：接入 OpenTelemetry（Meter 名称 `LabelService`、ASP.NET Core 和运行时指标、链路）；补 `label_queue_wait_ms`、`label_cache_hit_ratio`
- `/metrics` 端点
- 新建 `backend/deploy/monitoring/`：Grafana 仪表盘 JSON、Prometheus 告警规则（设计书第 9 节的 6 条）

## 共享文件

- `backend/Directory.Packages.props`：OpenTelemetry 包。Prometheus 导出器目前只有预发布版，引入前写 ADR
- `backend/src/LabelService.Server/Program.cs`

## 验收标准

- [ ] 导出的指标名和设计书一致（注意导出器会加单位后缀，必要时调整 Meter 里的名字并同步设计书）
- [ ] 仪表盘有：单张 P50/P95/P99、各阶段耗时、请求量与错误率（按调用方）、生成张数、并发与排队、缓存命中率、进程资源
- [ ] 告警规则：P95 > 100 ms 持续 5 分钟、P99 > 300 ms 或任一 > 2 s、5xx > 1%、调用方 4xx > 20%、健康检查连续失败、CPU > 80% 或排队 P95 > 50 ms
- [ ] 指标维度里没有 requestId、数据值等高基数字段
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 企业微信 / 钉钉通知的对接配置（部署时做）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
