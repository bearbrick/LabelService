# 性能约定

核心指标：**单张标签（≤ 100×150 mm、≤ 300 dpi）服务端处理 P95 ≤ 100 ms**，P99 ≤ 200 ms。“服务端处理”从收到完整请求算到写完最后一个字节。完整目标见设计书第 9 节。

## 目标

| 场景 | 目标 |
| --- | --- |
| 单张标签，服务端 | P95 ≤ 100 ms，P99 ≤ 200 ms |
| 单张标签，局域网调用方实测 | P95 ≤ 150 ms |
| 单张 A4 / A5 唛头（300 dpi） | P95 ≤ 250 ms |
| A4 拼版，一页不超过 24 张标签（PDF） | 每页 P95 ≤ 250 ms（T-018） |
| 批量 100 张，合并 PDF 或 ZPL | ≤ 3 s |
| 批量 500 张，合并 PDF | ≤ 10 s |
| 吞吐（4 核 8 GB 单实例，单张箱标 PNG） | ≥ 60 次/秒 |
| 冷启动 | 30 秒内完成预热，预热前就绪检查不通过 |
| 设计器 | 100 个元素时拖拽 ≥ 50 帧/秒 |

## 单张 100 ms 的预算

| 阶段（Server-Timing） | 预算 | 说明 |
| --- | --- | --- |
| `auth` + `tpl` + `validate` | 5 ms | 全部查内存 |
| `layout` | 10 ms | 文本测量、条码编码 |
| `draw` | 30 ms | SkiaSharp 绘制 |
| `encode` | 35 ms | PNG / PDF / ZPL 编码 |
| 写回响应 | 5 ms | Server-Timing 头先于响应体发出，头里的 total 不含这一段；调用日志、指标的总耗时含这一段（见下） |
| 余量 | 15 ms | |

## 热路径规则

“热路径”指开放接口从收到请求到写回响应的全部代码，包括 Rendering 里的一切。

1. **不碰数据库、磁盘、网络。** 模板、调用方、字体、图标、素材在启动或发布时加载进内存。
2. **不在请求里做一次性初始化**：字体加载、正则构造、反射、JSON 选项创建都放到静态字段或启动时。正则用 `[GeneratedRegex]`。
3. **计时用 `Stopwatch.GetTimestamp()`**，不用 `DateTime.Now`；新增阶段时同时改 `TimingStages`、本文和 docs/contracts.md。
4. **少分配**：大缓冲区用 `ArrayPool`；结果直接写响应流（`RenderResult.Content` 指向 MemoryStream 的缓冲区，不再复制）；避免 LINQ 出现在逐元素、逐像素的循环里。
5. **SKPaint、SKFont 等对象**：目前每次绘制新建，T-001 起按“字体 + 字号”等键缓存，缓存必须线程安全。
6. **条码矩阵按内容缓存**，批量里公共字段的条码只编码一次（T-002）。
7. **PNG 按内容选最小的无损格式**（全是黑白灰时 8 位灰度、有彩色时 8 位 RGB、mono 时 1 位，都不带 alpha），用 .NET 自带的 zlib 最快档压缩、不做行过滤（`Output/PngWriter.cs`，T-021）。改编码参数要附基准对比。
8. **并发**：渲染并发数等于 CPU 核数，排队超过 2 秒返回 503（T-017）。不要在渲染代码里 `Task.Run` 或加锁。

## 怎么测

**看单次请求**：每个响应的 `Server-Timing` 头列出各阶段耗时；服务端日志每次调用一行，`slow=True` 表示单张超过 100 ms。

**计时口径**（T-021）：服务端处理时间从开始处理请求算到写完最后一个字节。响应头必须先于响应体发出，所以 `Server-Timing` 的 `total` 只能算到写响应头为止；服务端日志的 `total`、调用日志 `render_log.duration_ms`、指标 `label_render_duration_ms` 和慢调用判断都在响应写完后（`HttpResponse.OnCompleted`）计算，包含写响应体的时间（`Endpoints/RenderCall.cs`）。后端检查 `TimingChecks` 验证：客户端拿到响应头后停 800 ms 再读约 1 MB 的响应体，调用日志的耗时 ≥ 800 ms，头里的 total 不含这段。验收压测要以日志或指标为准，头里的 total 偏小。

**基准测试**（BenchmarkDotNet，必须 Release）：

```powershell
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --filter "*"
```

改渲染代码（Rendering 下任何文件）的提交必须附上改动前后的基准结果。

基准覆盖成品箱标（`RenderBenchmarks`：203、300 dpi × PNG、PDF、ZPL，加一项每次换箱号、条码缓存不命中的 PNG）和每个示例模板 × PNG、PDF、ZPL（`TemplateBenchmarks`，模板自己的 DPI）。新增示例模板后把它加进 `TemplateBenchmarks.Template` 的参数。

**基准门禁**（T-015）：中位数比基线慢 20% 以上就不通过（退出码 1），CI 每次提交跑一遍。门禁先列出这次应该跑的全部基准，再看结果（T-021）：有基准没跑出结果（构建失败、运行出错）、过滤后一项都没有、不带 `--filter` 时少了基线里的项，都算没跑全，退出码 3，不算通过，也不写基线；基线里没有的新基准只打印、不比较。每项同时打印中位数和 P95（门禁按中位数比较，P95 只供记录）。`--self-test` 检查这些覆盖规则。

```powershell
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --gate               # 和 baseline.json 比较，约 5 分钟
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --gate --filter "*Zpl*"
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --update-baseline    # 写新基线
```

- 基线在 `backend/tests/LabelService.Benchmarks/baseline.json`，记了测基线的机器。**基线和机器有关**：要在跑门禁的那台（或同规格的）机器上生成，换机器先 `--update-baseline`。
- 什么时候更新基线：有意的性能改进合并后（让门禁跟着收紧），或者有意接受的变慢（在交接记录里写明原因）之后。更新基线单独提交：`perf(bench): update baseline (T-xxx)`。
- 门禁用 ShortRun 加到 10 次迭代：同一台机器连跑两次差 ±4% 以内；3 次迭代时能差到 25%，会误报。跑门禁时机器上不要有别的重负载（编译、视频会议）。
- CI 平台还没定（见 README 待定问题）：选定后加一步 `--gate`，并在 CI 机器上生成基线。CI 上要先准备字体（`backend/assets/fonts`），否则文本相关的检查和基准会失败。

**压测**（T-015）：k6 脚本在 `backend/tests/load/`，用法和报告模板见那里的 README。验收：在 4 核 8 GB 目标机器上 `k6 run single.js`（50 个并发持续发 60 秒），到写完最后一个字节的 P95 ≤ 100 ms（`log-report.cs` 从调用日志算）、k6 的 P95 ≤ 100 ms（同机）或 ≤ 150 ms（局域网）、错误率 0、吞吐 ≥ 60 次/秒（T-021）。

## 当前基线

| 日期 | 场景 | 结果 | 备注 |
| --- | --- | --- | --- |
| 2026-10-09 | 本机开发环境，单张箱标 PNG 300dpi（只有外框和横线），热请求 | total ≈ 10.5 ms（encode ≈ 9.5 ms） | 骨架阶段，文本、条码尚未实现 |
| 2026-10-09 | 同上，服务启动后第一次请求 | total ≈ 61 ms | 还没有预热（T-017） |
| 2026-10-09 | BenchmarkDotNet（`--job short`），`BoxLabelPng`，不含 HTTP | 203dpi 6.1 ms、300dpi 13.2 ms；每次分配 10–17 KB | 同上，耗时主要在 PNG 编码 |

| 2026-10-09 | BenchmarkDotNet（`--job short`），`BoxLabelPng`，加上 6 个文本元素（T-001，Noto Sans SC 可变字体） | 203dpi 5.65 ms、300dpi 11.91 ms；每次分配 49–66 KB | 同一台机器上改动前 5.22 ms、11.02 ms（+8%）；耗时仍主要在 PNG 编码 |
| 2026-10-09 | 本机开发环境，单张箱标 PNG 300dpi（外框、横线、6 个文本），热请求 | total 中位数 11.4 ms，最大 14.5 ms | 第一次请求 62 ms（layout 20 ms，首次栅格化字形），等 T-017 预热 |

| 2026-10-09 | BenchmarkDotNet（`--job short`），`BoxLabelPng`，加上一维码（T-002） | 203dpi 5.55 ms、300dpi 11.91 ms；每次分配 56–74 KB | 同一台机器上改动前 5.56 ms、11.94 ms，基本不变；基准里箱号不变，编码走缓存，不走缓存时编码一次是微秒级 |

| 2026-10-09 | BenchmarkDotNet（`--job short`），`BoxLabelPng`，加上二维码（T-003） | 203dpi 6.14 ms、300dpi 12.49 ms；每次分配 59–78 KB | 改动前 5.55 ms、11.91 ms（+5%～+10%），多出的主要是 PNG 编码 |

| 2026-10-09 | BenchmarkDotNet（`--job short`），`BoxLabelPng`，加上图标（T-004，示例箱标的全部元素都已实现） | 203dpi 5.96 ms、300dpi 12.93 ms；每次分配 60–79 KB | 改动前 6.14 ms、12.49 ms，在误差范围内 |

| 2026-10-09 | BenchmarkDotNet（`--job short`），`BoxLabelPdf`（T-005） | 203dpi 3.59 ms、300dpi 3.70 ms；每次分配约 228 KB | 矢量输出，不栅格化、不做 PNG 压缩，比 PNG 快；PNG 同一轮 5.68 ms、12.24 ms |

| 2026-10-09 | BenchmarkDotNet（`--job short`），`BoxLabelZpl`（T-006） | 203dpi 1.09 ms、300dpi 2.28 ms；每次分配 153–255 KB | 黑白位图 + ZPL ASCII 压缩，203dpi 示例箱标约 10.8 KB；同一轮 PNG 6.07 ms、12.70 ms，PDF 3.75 ms、3.63 ms |

| 2026-10-09 | 批量接口（T-007），100 张示例箱标合并 PDF，进程内服务（检查程序），热请求 | 约 50 ms（layout 5.5、draw 6.0、encode 37.2 ms） | 要求 ≤ 3 s；批量 zip 时 PNG 按 CPU 核数并行渲染 |

| 2026-10-09 | 调用日志查询（T-009），label-log.db 100 万行，按模板、调用方、结果、时间筛选取一页 50 条 | 最慢 1.1 ms | 要求 < 200 ms；每种筛选条件和时间组成复合索引，翻页用 keyset；检查程序默认灌 20 万行，`LABEL_LOG_PERF_ROWS=1000000` 测 100 万行 |

| 2026-10-09 | BenchmarkDotNet（`--job short`），`BoxLabelPng` 和新增的 `BoxLabelPngUniqueBox`（每次箱号不同，条码编码缓存不命中），二维码改为混合模式最小编码（T-016） | 固定箱号 203dpi 5.52 ms、300dpi 11.55 ms；每次换箱号 5.84 ms、11.70 ms，每次分配 174–191 KB | 改动前（同一轮）固定箱号 5.52 ms、11.71 ms，换箱号 5.82 ms、12.05 ms、分配 121–140 KB：耗时在误差范围内，最小编码每次多分配约 50 KB |

| 2026-10-09 | 服务端预览（T-016），`POST /api/admin/preview` 示例箱标 300dpi，本机开发服务，热请求 | 服务端约 20–55 ms（含 PNG 编码），往返 36–94 ms | 只给设计器用，不在开放接口的 100 ms 预算里；设计器画布和服务端 PNG 的元素位置最大偏差 0.085 mm（1 个点），要求 ≤ 0.2 mm |

| 2026-10-10 | 启动预热（T-017），Release 进程冷启动，已有数据库（不导入示例），示例箱标 PNG 300dpi，Server-Timing total | 预热后第一个请求 18.3 ms，之后 11.3–12.4 ms（约 1.6 倍）；进程启动到 /health/ready 约 1.45 s | 关掉预热时第一个请求 64 ms（约 5.6 倍）；要求不超过 2 倍、30 秒内就绪 |

| 2026-10-10 | 渲染并发限制（T-017），检查程序占满名额后发请求 | 排队 2.0 s 后返回 503 SERVICE_UNAVAILABLE，带 Retry-After: 1 | 名额 = CPU 核数；排队时间进 label_queue_wait_ms |

| 2026-10-10 | 基准门禁基线（T-015，`baseline.json`），ShortRun 10 次迭代，AMD Ryzen AI 9 H 465（20 逻辑核） | 箱标 PNG 203dpi 5.49 ms、300dpi 11.66 ms；PDF 3.10 / 2.97 ms；ZPL 0.99 / 2.01 ms；A4 唛头 300dpi：PDF 6.26 ms、ZPL 19.8 ms、PNG 115 ms | A4 唛头 PNG 主要花在 PNG 编码（2480×3508 像素），在“A4 唛头 P95 ≤ 250 ms”以内；A4 建议用 PDF。连跑两次门禁差 ±4% |

| 2026-10-10 | k6 压单张接口（T-015），本机模拟 4 核：服务进程绑定 4 个核（start /affinity F），渲染并发 4，Release，示例箱标 PNG 300dpi | 固定 60 次/秒（最多 50 并发）：服务端 P95 15.1 ms、P99 15.7 ms，错误 0；150 次/秒：P95 17.0 ms；不停地发时最大吞吐约 215 次/秒 | 50 个并发不停地发（饱和）时排队时间算进耗时，P95 251 ms，仍没有 503；目标机器（4 核 8 GB 服务器）上要再测一次，结果按 backend/tests/load/REPORT.md 记录 |

| 2026-10-10 | k6 压批量接口（T-015），同上 4 核，100 张示例箱标合并 PDF，4 个并发 30 秒 | 服务端中位数 41.8 ms、P95 66.5 ms，错误 0，约 82 批/秒 | 要求 100 张 ≤ 3 s |

| 2026-10-10 | A4 拼版（T-018，`SheetBenchmarks`），一页 24 张 70×37 mm 小箱标（每张 12 个元素、箱号各不相同），300 dpi，ShortRun 10 次迭代，同上机器 | PDF 中位数 7.3 ms（预算 250 ms）；PNG 122 ms（A4 整页 PNG 编码为主，和 A4 唛头 PNG 相当） | 固定元素只排版一次（`LabelPlan`）：PDF 7.31 ms 对 7.31 ms，时间看不出差别（文本测量、条码编码本来就有缓存），分配从 1078 KB 降到 875 KB（−19%）。门禁只放 PDF 两项，PNG 太重会让笔记本降频、拖慢后面的基准（整套门禁跑完时 TemplateBenchmarks 曾误报慢 30%，单独重跑 ±3%） |

| 2026-10-10 | PNG 编码改为 `PngWriter`（T-021），完整门禁、基线不变，ShortRun 10 次迭代，同上机器 | 箱标 PNG 203dpi 5.49 → 0.94 ms、300dpi 11.66 → 1.82 ms；换箱号 5.75 → 1.21、11.60 → 2.02 ms；A4 唛头 PNG 115.3 → 15.9 ms；A4 拼版 PNG 124.8 → 23.1 ms（P95 128.1 → 23.9） | 原来 Skia 编码 RGBA 占单张总耗时近九成。输出逐像素一致（后端检查 `PngChecks`），示例箱标从 RGBA 49 KB 变成 8 位灰度 40 KB。其余 PDF、ZPL 各项比基线 +2%～+12%（没改这些代码，属笔记本温度、负载波动，门禁 20% 以内）。按验收要求没有更新基线，改进后要不要收紧基线由人决定。证据 `docs/reviews/phase1-evidence/gate-full-pngwriter.txt` |

| 2026-10-10 | 单张请求 Server-Timing（T-021），Release，示例箱标 PNG 300dpi，低负载热请求 | 修复前 draw 1.0–1.3、encode 10.7–13.5、total 12–15 ms；修复后 draw 1.2–1.5、encode 0.8–0.9、total 2.6–3.3 ms | |

| 2026-10-10 | k6 单张验收压测（T-021，`single.js` 默认：50 个并发持续 60 秒，每次换箱号），Release，示例箱标 PNG 300dpi，服务进程从启动起绑核，k6 绑在另外的核 | 修复前（绑 2 个物理核）：213 次/秒，k6 P95 296.9 ms、Server-Timing P95 253.3 ms，**不通过**。修复后绑 2 个物理核（4 个逻辑核）：986 次/秒，k6 P95 63.8 ms，到最后一个字节 P95 58.2 ms、P99 67.6 ms；绑 4 个物理核：1086 次/秒，k6 P95 75.1 ms，到最后一个字节 P95 63.7 ms、P99 98.1 ms；错误都是 0 | 50 个并发不停地发时排队时间算进耗时，上一行 T-015 的“饱和 P95 251 ms”就是这个口径没过。本机是 Ryzen AI 9 H 465 笔记本，**4 核 8 GB 目标机器上还没测**（环境阻塞）。证据 `docs/reviews/phase1-evidence/load-50vu.txt` |

| 2026-10-10 | A4 拼版（T-021 复核 T-018），`SheetBenchmarks` PDF、PNG 都在门禁里，同上 | PDF 中位数 8.05 ms、P95 8.15 ms；PNG（`PngWriter` 后）中位数 23.1 ms、P95 23.9 ms；预算是 P95 ≤ 250 ms | 固定元素共享排版（`ShareStatic`）**没有带来可测的提速**：PDF 两次完整门禁 −3%、−5%，PNG +1%、+2%，都在同机 ±4% 的波动内；只是 PDF 每次分配少 19%。T-018 行里“PNG 太重、门禁只放 PDF”已改回 PDF、PNG 都放（T-021）。上面 T-018 行的 7.3 ms 是中位数，不是 P95 |

实现新元素或格式后在这里追加一行，写明机器和条件。
