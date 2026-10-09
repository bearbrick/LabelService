# T-017 预热、并发控制与部署

状态：待验收
负责人：Claude 2026-10-10
里程碑：M4
依赖：T-008（预热要遍历已发布模板）
设计书：§9 怎么做到、其他非功能需求；ADR 0003

## 目标

服务启动后第一个请求也不慢；高峰时请求排队而不是一起变慢；能用 Docker 或 Windows 服务单实例部署，数据有每日备份。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Server/Hosting/`：启动预热（加载字体、图标、素材，用样例值把每个已发布模板各渲染一次）、就绪检查、渲染并发限制
- 新建 `backend/deploy/`：Dockerfile（Linux，字体随镜像打包，数据目录挂卷）、Windows 服务安装说明
- 每日备份：后台服务用 `VACUUM INTO` 给 `label.db`、`label-log.db` 生成快照，保留份数可配置
- `backend/src/LabelService.Server/LabelService.Server.csproj`：发布配置（ReadyToRun 需要指定运行时标识）

## 共享文件

- `backend/src/LabelService.Server/Program.cs`
- `backend/src/LabelService.Contracts/Protocol/ErrorCodes.cs`：排队超时的 503 如需新错误码，按契约变更流程处理（SDK 已把 503 统一映射为 SERVICE_UNAVAILABLE 并重试）
- 实际改了：`Program.cs`、`Endpoints/PublicApiEndpoints.cs`（排队）、`ErrorCodes.cs`、`client/.../LabelErrorCodes.cs`（注释）、`docs/contracts.md`、设计书第 8 节错误表（同步 503）、`backend/Directory.Packages.props`（WindowsServices）、`AGENTS.md` 第 1 节

## 验收标准

- [x] 预热完成前 `/health/ready` 不通过；30 秒内完成预热
- [x] 启动后第一个请求 total 与热请求相差不超过 2 倍（当前骨架：61 ms 对 10 ms）
- [x] 渲染并发数 = CPU 核数，多出的排队；排队超过 2 秒返回 503；排队时间进 `label_queue_wait_ms`
- [ ] Docker 镜像在没有任何系统中文字体的 Linux 上渲染中文正常（未验证：开发机没有 Docker，见交接记录）
- [x] 备份快照能直接替换库文件恢复（写进部署说明并实际演练一次）
- [x] 部署说明写明：数据目录不能放网络共享盘，同一数据目录只能有一个服务进程
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- Kubernetes 编排、多实例与高可用（ADR 0003：前期单实例）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-10 · Claude · 完成，待验收（Docker 一项没法在本机验证）。
  - 预热（`Hosting/WarmupService`）：后台线程解析一次请求体 JSON，再用样例值把每个已发布模板的最新版本按 PNG、PDF、ZPL 各渲染一次（模板 DPI），最多 25 秒；完成后 `/health/ready` 才 200（`/health` 只看存活）。实测 Release 冷启动、已有数据库：第一个请求 18.3 ms、热请求约 11.5 ms（1.6 倍）；关掉预热是 64 ms（5.6 倍）。进程启动到就绪约 1.45 s。
  - 并发（`Hosting/RenderGate`）：信号量，名额 = CPU 核数（可配），单张、批量各占一个名额；排队超过 2 秒返回 503 `SERVICE_UNAVAILABLE` + `Retry-After: 1`；排队时间进 `label_queue_wait_ms`。管理接口的预览、接口调试不排队。
  - 错误码：`SERVICE_UNAVAILABLE` 加进 Contracts（503，向后兼容），SDK 本来就有这个码；contracts.md、设计书第 8 节错误表同步了一行。
  - 备份（`Hosting/BackupService`）：每天 02:30（可配）用 `VACUUM INTO` 给 label.db、label-log.db 做快照，连同 `assets/` 放到 `backups/yyyyMMdd-HHmmss/`，保留 7 份；快照改成 WAL 模式。恢复演练写进检查程序：做快照、删数据、停服务、替换库文件、重启，数据回来、生成正常。
  - 演练时发现的问题并修了：`VACUUM INTO` 出来的库是回滚日志模式，EF Core 检查库是否存在时开的是只读连接，`SqlitePragmaInterceptor` 在只读连接上设 WAL 会报 attempt to write a readonly database，服务起不来。现在只读连接不设 journal_mode，快照也先改成 WAL。
  - 部署（ADR 0008）：`backend/deploy/Dockerfile`（aspnet:10.0，linux-x64，ReadyToRun，/data 挂卷，app 用户）、`deploy/README.md`（两条硬规定、配置表、健康检查、Docker、Windows 服务、前端反向代理、备份与恢复步骤）。程序里加了 `UseWindowsService`（新依赖 Microsoft.Extensions.Hosting.WindowsServices 10.0.12）。发布时指定 -r 就打开 ReadyToRun。
  - 检查：新增 `HostingChecks.cs`（9 项：排队和超时、默认名额、就绪检查、占满名额后 503 和 Retry-After、释放后恢复、快照和保留份数、快照是 WAL 的完整库、恢复演练、非 WAL 库也能启动）。
  - 没验证的：Docker 镜像没构建过（本机没有 Docker、也没有 WSL 发行版），只验证了 `dotnet publish -r linux-x64` 能出包、原生库和字体都在；Windows 服务没实际安装（要管理员权限）。请在有 Docker 的机器上构建一次，用示例请求生成一张 PNG 看中文。
