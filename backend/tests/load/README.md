# 压测（k6）

单张接口和批量接口的 k6 脚本。需要已安装的 k6，在本目录运行。先确认目标为专用压测或现场验收实例，**不要把默认 localhost:5080 当成安全的隔离测试环境**。默认使用开发 Key，仅 Development 有效；其他实例显式传 `-e BASE_URL=... -e API_KEY=...`。指标和当前异常见 [性能约定](../../../docs/performance.md)。

| 脚本 | 场景 | 门槛 |
| --- | --- | --- |
| `single.js` | 验收：单张接口 50 个并发持续发 60 秒（constant-vus），每次换箱号 | Server-Timing 和 k6 的 P95 都 ≤ 100 ms、错误率 0、每个响应都有 Server-Timing |
| `single.js -e MODE=rate -e RATE=150` | 按固定速率发（最多 50 个并发） | 同上，另要求没有发不出去的请求（达到这个速率） |
| `batch.js` | 批量接口，每次 100 张示例箱标合并 PDF，4 个并发 | 服务端 P95 < 3 s、错误率 0 |

```powershell
k6 run single.js
k6 run -e MODE=rate -e RATE=150 single.js              # 按固定速率
k6 run -e REPEAT=1 single.js                            # 每次发完全相同的请求（条码缓存全命中，偏乐观）
dotnet run log-report.cs "<数据目录>/label-log.db" <压测开始的 UTC 时间>   # 到写完最后一个字节的 P50/P95/P99
k6 run -e FORMAT=pdf single.js                          # 换输出格式
k6 run -e REQUEST=../../samples/requests/a4-shipping-mark.render.json -e FORMAT=pdf single.js
k6 run batch.js
```

“服务端耗时”的口径是从开始处理请求到写完最后一个字节（含排队，不含网络）。响应头 `Server-Timing` 的 `total` 只能算到写响应头为止（T-021），k6 按它打门槛是近似；正式结果用 `log-report.cs` 从调用日志 `render_log.duration_ms` 算（每秒上千次时也不丢条）。k6 自己的 `http_req_duration` 还包含网络；k6 和服务不在同一台机器、要按设计书“局域网调用方实测 P95 ≤ 150 ms”验时加 `-e HTTP_BUDGET=150`。

## 注意

- 压测机和服务器分开：k6 本身也吃 CPU，同一台机器上跑会互相影响。只有一台机器时，用 `start /affinity F` 从启动起把服务进程绑到 4 个核上（启动后再改亲和性不行：渲染并发名额按启动时的 CPU 数定），k6 也用 `start /affinity` 绑到剩下的核（本机测试就是这么做的，结果见 docs/performance.md）。
- 服务器用 Release 发布版本、`ASPNETCORE_ENVIRONMENT=Production`，日志级别保持默认；压测前等 `/health/ready` 变成 200（预热完）。
- 压测用一个专门的调用方，压完在管理后台停用；调用日志会记下全部请求，压测数据可以按调用方筛掉。
- 不要压生产环境。

## 报告

每次正式压测按 `REPORT.md` 的格式记一份（机器配置、版本、并发、P50/P95/P99、错误率、吞吐、结论），关键数字同步到 docs/performance.md 的“当前基线”。
