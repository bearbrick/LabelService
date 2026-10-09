# 0001 技术栈与仓库结构

状态：已采纳（“一个仓库、一个解决方案”的仓库结构部分已被 0004 取代）
日期：2026-10-09
关联：T-000

## 背景

标签服务要同时提供网页设计器、开放接口和给第三方的 .NET SDK，并且单张生成 P95 ≤ 100 ms。后续可能由多个 AI 助手并行开发，需要边界清楚、能自动检查的结构。

## 决定

- 后端 ASP.NET Core，.NET 10 LTS（.NET 8 在 2026 年 11 月停止支持）。
- 绘图 SkiaSharp 4.x（一套代码输出 PNG 和矢量 PDF），条码 ZXing.Net，SVG 图标 Svg.Skia。
- 前端 Vue 3 + TypeScript + Vite，画布 Konva + vue-konva，浏览器端条码预览 bwip-js。
- 一个仓库、一个解决方案 `LabelService.slnx`，按职责拆成 Contracts、Rendering、Server、Client 四个产品项目；依赖只能 Contracts ← Rendering ← Server，Client 独立。
- 包版本集中在 `Directory.Packages.props`；`TreatWarningsAsErrors`；产品项目强制中文 XML 注释。
- 检查程序用控制台项目（沿用公司 DB Studio 的做法），不用测试框架；基准用 BenchmarkDotNet。
- 数据库访问用 EF Core，具体数据库待确认（设计书第 12 节），在 T-008 决定后另写 ADR。

## 备选方案

- System.Drawing：只适合 Windows，.NET 6 起在非 Windows 上不再支持。
- QuestPDF：只出 PDF，PNG 和 ZPL 还得另写一套绘图。
- 设计器用 Fabric.js：功能相近，但 Konva 的数据驱动写法和 Vue 结合更自然，模板 JSON 可以直接当画布状态。

## 影响

- Linux 部署需要 `SkiaSharp.NativeAssets.Linux.NoDependencies`（已在 Server 引用）。
- 字体只打包思源黑体、思源宋体（SIL OFL），不打包微软雅黑、宋体。
- OpenTelemetry 的 Prometheus 导出器目前只有预发布版，接入时（T-010）需要另行确认。
