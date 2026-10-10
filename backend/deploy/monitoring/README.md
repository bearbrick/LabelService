# 监控

标签服务在 `GET /metrics` 输出 Prometheus 文本格式（ADR 0009），这里是抓取配置、告警规则和 Grafana 仪表盘的样例。监控平台已定为随服务自建 Prometheus + Grafana（2026-10-11），部署时直接从这些文件开始；告警发到哪个群还没定，`alerts.yml` 只有规则，没配通知渠道。

| 文件 | 用途 |
| --- | --- |
| `prometheus.yml` | 抓取配置样例：每 15 秒抓一次 `/metrics`，引用 `alerts.yml` |
| `alerts.yml` | 告警规则：设计书第 9 节的 6 条（单张变慢、严重变慢、5xx 占比、调用方 4xx 占比、服务不可用、资源紧张） |
| `grafana-dashboard.json` | Grafana 仪表盘：延迟（P50/P95/P99、各阶段、最慢的模板）、调用量和错误率（按调用方）、生成张数、并发和排队、缓存命中率、CPU、内存、GC、线程池。导入时选 Prometheus 数据源 |

`/metrics` 不需要登录，只在内网开放；反向代理不要把它转到外网。

## 指标

本服务的指标（名字和设计书第 9 节一致）：

| 指标 | 类型 | 维度 | 说明 |
| --- | --- | --- | --- |
| `label_render_duration_ms` | 直方图 | endpoint、template、format、dpi、result | 服务端处理一次请求的总耗时（= Server-Timing 的 total）。endpoint 有 render、batch、preview（设计器预览，不算开放接口） |
| `label_render_stage_ms` | 直方图 | stage、format | 各阶段耗时：auth、tpl、validate、layout、draw、encode（不含预览） |
| `label_requests_total` | 计数器 | endpoint、client、result | 请求数；result 是 ok 或错误码 |
| `labels_generated_total` | 计数器 | template、format | 生成的标签张数（含份数） |
| `label_inflight_requests` | 仪表 | — | 正在处理的生成请求数 |
| `label_queue_wait_ms` | 直方图 | — | 等渲染名额的时间；超过 2 秒的请求返回 503 |
| `label_cache_hit_ratio` | 仪表 | cache | 缓存命中率（进程启动以来）：templates、fonts、images、symbols |
| `label_log_dropped_total` | 计数器 | — | 调用日志队列满被丢弃的条数 |

另外有运行时（`dotnet_process_cpu_time_seconds_total`、`dotnet_process_memory_working_set_bytes`、`dotnet_gc_*`、`dotnet_thread_pool_*`）和 ASP.NET Core（`http_server_request_duration_seconds`、`http_server_active_requests`）的指标。注意：运行时把线程池的 `queue.length`、`thread.count` 定义成计数器，所以名字带 `_total`，值本身是当前数量。

维度里没有 requestId、traceId、数据值、来源 IP 这类取值无限的字段（检查程序会核对）。

## 常用查询

```promql
# 单张接口 P95（毫秒）
histogram_quantile(0.95, sum by (le) (rate(label_render_duration_ms_bucket{endpoint="render"}[5m])))
# 各调用方的错误率
sum by (client) (rate(label_requests_total{endpoint=~"render|batch", result!~"ok|aborted"}[5m]))
  / sum by (client) (rate(label_requests_total{endpoint=~"render|batch"}[5m]))
# CPU 使用率
sum(rate(dotnet_process_cpu_time_seconds_total[5m])) / max(dotnet_process_cpu_count)
```

## 告警通知

`alerts.yml` 的规则带 `severity`（warning / critical）。企业微信、钉钉群通知在 Alertmanager 里按 severity 路由，部署时配置；“服务不可用”另外建议用 blackbox exporter 探测 `/health/ready`。
