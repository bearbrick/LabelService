# 压测（k6）

单张接口和批量接口的 k6 脚本（T-015、设计书第 9 节“压测与防退化”）。需要 [k6](https://k6.io)。在本目录运行，默认请求 `http://localhost:5080`、开发 Key（只在 Development 环境有效）；压别的服务器用 `-e BASE_URL=... -e API_KEY=...`。

| 脚本 | 场景 | 门槛 |
| --- | --- | --- |
| `single.js` | 单张接口，默认按固定速率 60 次/秒、最多 50 个并发请求，跑 60 秒 | 服务端 P95 < 100 ms、错误率 0、没有发不出去的请求（达到 60 次/秒） |
| `single.js -e MODE=saturate` | 50 个并发不停地发，测最大吞吐 | 错误率 0（排队时间会算进耗时，不设 P95 门槛） |
| `batch.js` | 批量接口，每次 100 张示例箱标合并 PDF，4 个并发 | 服务端 P95 < 3 s、错误率 0 |

```powershell
k6 run single.js
k6 run -e RATE=150 -e DURATION=120s single.js          # 找 P95 还在 100 ms 以内的最高速率
k6 run -e MODE=saturate -e VUS=50 single.js             # 最大吞吐
k6 run -e FORMAT=pdf single.js                          # 换输出格式
k6 run -e REQUEST=../../samples/requests/a4-shipping-mark.render.json -e FORMAT=pdf single.js
k6 run batch.js
```

“服务端耗时”取响应头 `Server-Timing` 的 `total`（从收到请求到生成完，含排队，不含网络），对应设计书的 P95 ≤ 100 ms；k6 自己的 `http_req_duration` 还包含网络。

## 注意

- 压测机和服务器分开：k6 本身也吃 CPU，同一台机器上跑会互相影响。只有一台机器时，用 `start /affinity F` 把服务进程绑到 4 个核上，k6 用剩下的核（本机测试就是这么做的，结果见 docs/performance.md）。
- 服务器用 Release 发布版本、`ASPNETCORE_ENVIRONMENT=Production`，日志级别保持默认；压测前等 `/health/ready` 变成 200（预热完）。
- 压测用一个专门的调用方，压完在管理后台停用；调用日志会记下全部请求，压测数据可以按调用方筛掉。
- 不要压生产环境。

## 报告

每次正式压测按 `REPORT.md` 的格式记一份（机器配置、版本、并发、P50/P95/P99、错误率、吞吐、结论），关键数字同步到 docs/performance.md 的“当前基线”。
