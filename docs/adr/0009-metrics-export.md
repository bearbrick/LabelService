# 0009 指标导出：自己实现 Prometheus 文本格式，暂不引入 OpenTelemetry 包

状态：提议
日期：2026-10-10
关联：T-010、ADR 0008

## 背景

设计书第 9 节：服务用 .NET 自带的指标和链路 API（OpenTelemetry 标准）采集，导出到 Prometheus，提供 `/metrics`。T-010 的范围写的是“接入 OpenTelemetry”，同时提醒：OpenTelemetry 的 Prometheus 导出器（`OpenTelemetry.Exporter.Prometheus.AspNetCore`）一直只有预发布版，引入前要写 ADR。AGENTS.md R8 要求依赖选发布满两周的稳定版。监控平台（公司现有平台还是自建 Prometheus + Grafana）也还没定。

## 决定

- 指标仍然只用 .NET 自带的 `System.Diagnostics.Metrics`（这就是 OpenTelemetry 的指标 API），名字和维度按设计书。
- `/metrics` 由服务自己的 `PrometheusExporter` 输出 Prometheus 文本格式（0.0.4）：用 `MeterListener` 订阅本服务的 `LabelService`、ASP.NET Core 的 `Microsoft.AspNetCore.Hosting`、运行时的 `System.Runtime`，计数器累加、仪表取当前值、直方图按固定分桶。约 300 行，不新增任何依赖。
- 本服务的指标名原样导出（已经是 Prometheus 风格，和设计书一致，不加单位后缀）；其他 Meter 的名字按 OpenTelemetry 转 Prometheus 的习惯转换（点换下划线、加 `_seconds`/`_bytes`、计数器加 `_total`），和以后换成官方导出器时的名字基本一致。
- 链路（trace）暂不导出：响应头和调用日志里已经有 traceId，等监控平台定了再决定导出到哪里（OTLP）。

## 备选方案

- **OpenTelemetry SDK + 预发布的 Prometheus 导出器**：功能全、以后接 OTLP 方便，但违反 R8（预发布版），而且要四五个包。官方导出器出稳定版、或者确定用公司平台（OTLP）时再换，换的时候只改 `Diagnostics/`，指标本身不用动。
- **OpenTelemetry SDK + OTLP 导出到 Collector，由 Collector 转 Prometheus**：多部署一个组件，平台没定之前不值得。
- **prometheus-net**：成熟的第三方库，但它有自己的一套指标 API，要把现有指标改写一遍，和设计书“用 .NET 自带 API”不一致。

## 影响

- `/metrics` 不需要登录，只能在内网开放（部署说明里反向代理不转发它）。
- 仪表盘和告警规则在 `backend/deploy/monitoring/`，检查程序核对它们用到的指标都存在。
- 以后换官方导出器时，运行时、ASP.NET Core 指标的名字可能有细微差别（如 `_total` 后缀），仪表盘要跟着检查一遍。
