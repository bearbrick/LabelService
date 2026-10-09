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
| 写回响应 | 5 ms | 不计入 Server-Timing |
| 余量 | 15 ms | |

## 热路径规则

“热路径”指开放接口从收到请求到写回响应的全部代码，包括 Rendering 里的一切。

1. **不碰数据库、磁盘、网络。** 模板、调用方、字体、图标、素材在启动或发布时加载进内存。
2. **不在请求里做一次性初始化**：字体加载、正则构造、反射、JSON 选项创建都放到静态字段或启动时。正则用 `[GeneratedRegex]`。
3. **计时用 `Stopwatch.GetTimestamp()`**，不用 `DateTime.Now`；新增阶段时同时改 `TimingStages`、本文和 docs/contracts.md。
4. **少分配**：大缓冲区用 `ArrayPool`；结果直接写响应流（`RenderResult.Content` 指向 MemoryStream 的缓冲区，不再复制）；避免 LINQ 出现在逐元素、逐像素的循环里。
5. **SKPaint、SKFont 等对象**：目前每次绘制新建，T-001 起按“字体 + 字号”等键缓存，缓存必须线程安全。
6. **条码矩阵按内容缓存**，批量里公共字段的条码只编码一次（T-002）。
7. **PNG 用最低压缩等级、不做行过滤**，以体积换速度。改编码参数要附基准对比。
8. **并发**：渲染并发数等于 CPU 核数，排队超过 2 秒返回 503（T-017）。不要在渲染代码里 `Task.Run` 或加锁。

## 怎么测

**看单次请求**：每个响应的 `Server-Timing` 头列出各阶段耗时；服务端日志每次调用一行，`slow=True` 表示单张超过 100 ms。

**基准测试**（BenchmarkDotNet，必须 Release）：

```powershell
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --filter "*"
```

改渲染代码（Rendering 下任何文件）的提交必须附上改动前后的基准结果。中位数比基线慢 20% 以上不能合并（CI 门禁见 T-015）。

**压测**：k6 或 NBomber 在目标机器上 50 并发压单张接口，要求 P95 ≤ 100 ms、错误率 0（T-015）。

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

实现新元素或格式后在这里追加一行，写明机器和条件。
