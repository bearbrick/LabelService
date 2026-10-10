# T-010 监控接入

状态：待验收
负责人：Claude 2026-10-10
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

- [x] 导出的指标名和设计书一致（注意导出器会加单位后缀，必要时调整 Meter 里的名字并同步设计书）
- [x] 仪表盘有：单张 P50/P95/P99、各阶段耗时、请求量与错误率（按调用方）、生成张数、并发与排队、缓存命中率、进程资源
- [x] 告警规则：P95 > 100 ms 持续 5 分钟、P99 > 300 ms 或任一 > 2 s、5xx > 1%、调用方 4xx > 20%、健康检查连续失败、CPU > 80% 或排队 P95 > 50 ms
- [x] 指标维度里没有 requestId、数据值等高基数字段
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 企业微信 / 钉钉通知的对接配置（部署时做）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-10 · Claude（T-021 验收复核）· 如实补充：ADR 0009（自己实现 /metrics 的 Prometheus 文本格式、不引入 OpenTelemetry 包）仍是“提议”，等用户确认。上面勾选的仪表盘、告警规则指的是 `backend/deploy/monitoring/` 里的文件和检查程序里的格式、名称核对；**没有接真实的 Prometheus 抓取、没有导入 Grafana、没有触发过告警**（本机没有这些服务），链路（trace）只有 `X-Trace-Id`（Activity），没有导出到任何链路系统。这些属于环境阻塞，不算已验证。另：T-021 起 `label_render_duration_ms` 和慢调用都按“写完最后一个字节”计时。
- 2026-10-10 · Claude · 完成，待验收。和任务原文有一处不同，请确认 ADR 0009。
  - 没有接 OpenTelemetry 包：它的 Prometheus 导出器只有预发布版（R8 不允许），监控平台也没定。指标本来就用 .NET 自带的 `System.Diagnostics.Metrics`（OpenTelemetry 的指标 API），`/metrics` 由 `Diagnostics/PrometheusExporter` 自己输出 Prometheus 文本格式（MeterListener 订阅 LabelService、Microsoft.AspNetCore.Hosting、System.Runtime 三个 Meter，前两个只认本主机的，检查程序里多个主机不串）。本服务的指标名原样导出，和设计书一致；没有新增依赖。链路（trace）暂不导出，响应和日志里已有 traceId。ADR 0009（提议）。
  - `label_queue_wait_ms` 在 T-017 已补；新增 `label_cache_hit_ratio`（templates、fonts、images、symbols）：Rendering 里 `RenderCaches` 用 Interlocked 计命中和未命中，模板存储计查找，服务端用可观测仪表报命中率。改了 Rendering，基准没有变化（203dpi 5.48 ms、300dpi 11.46 ms，分配不变）。
  - 设计书第 9 节“工作台显示今日 P95、慢调用数、最慢的模板”：概览接口加了 `today.p95Ms`（今日单张成功调用，最近秩法）和 `slowTemplates`（近 7 天单张平均最慢的 5 个），工作台的 KPI 改成“单张 P95”，加了“近 7 天最慢的模板”。
  - `backend/deploy/monitoring/`：`prometheus.yml`（抓取样例）、`alerts.yml`（6 条规则）、`grafana-dashboard.json`（16 个面板，脚本生成）、README（指标表、常用查询）。
  - 检查：新增 `MetricsChecks.cs`（9 项：导出格式和分桶、名字转换、别的主机不混入、设计书指标都在、请求量按调用方和结果码、直方图分桶、缓存命中率、进程和 ASP.NET Core 指标、仪表盘和告警用到的指标都存在、维度没有高基数字段），概览 P95 一项。
  - 没做的：告警规则没有在真实的 Prometheus 上加载过（本机没有），PromQL 是按导出的指标名写的；企业微信、钉钉通知（不做）。
