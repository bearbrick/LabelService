# 0008 部署：Docker（Linux）或 Windows 服务，单实例，每日 VACUUM INTO 备份

状态：已采纳
日期：2026-10-10
关联：T-017、ADR 0003

## 背景

设计书第 9 节要求“能用 Docker 或 Windows 服务单实例部署，数据有每日备份”；ADR 0003 定了 SQLite、单实例、数据目录不能放网络盘。公司里既有 Linux 服务器也有 Windows 服务器，要两种都能部署。

## 决定

- **Docker**：`backend/deploy/Dockerfile`，基于官方 `mcr.microsoft.com/dotnet/aspnet:10.0`，`dotnet publish -r linux-x64`。镜像里只放思源黑体、思源宋体（不依赖系统字体，SkiaSharp 用 NoDependencies 原生库），数据目录 `/data` 挂卷，以非 root 的 `app` 用户运行。
- **Windows 服务**：程序里调用 `UseWindowsService()`，用 `New-Service` 安装。新增依赖 `Microsoft.Extensions.Hosting.WindowsServices`（微软官方包，10.0.12，和其他 ASP.NET Core 包同一版本线）；不是以服务方式启动时它什么也不做。
- **发布时预编译**：指定运行时标识发布时打开 ReadyToRun，缩短启动和第一个请求的 JIT。
- **启动预热、就绪检查**：预热完成前 `/health/ready` 返回 503；`/health` 只表示进程活着。
- **并发**：同时渲染数 = CPU 核数，多出的排队，排队超过 2 秒返回 503 `SERVICE_UNAVAILABLE`（新增的契约错误码，向后兼容；SDK 本来就把 503 映射成这个码并重试）。
- **备份**：服务内的后台任务每天用 `VACUUM INTO` 生成主库、日志库快照，连同上传的素材放到 `backups/时间戳/`，保留最近 7 份；快照改成 WAL 模式，恢复时直接替换库文件。
- **前端**：打包成静态文件，由 Nginx 或 IIS 托管，`/api`、`/fonts` 反向代理到服务。

## 备选方案

- **自己写 ServiceBase 包装**：省一个依赖，但官方包已经处理好服务控制管理器的启停、日志和内容根目录。
- **NSSM 之类的第三方服务包装器**：要额外装工具，日志和停止信号不如官方包可靠。
- **备份用外部计划任务复制文件**：直接复制正在写的 .db 可能不一致；`VACUUM INTO` 由 SQLite 保证一致性，服务自己做也不需要额外配置计划任务。
- **服务直接托管前端静态文件**：部署更简单，但前后端发布节奏不同，且以后多实例时前端放 CDN 或网关更合适；先用反向代理，需要时再加。

## 影响

- 新增 NuGet 依赖 `Microsoft.Extensions.Hosting.WindowsServices`。
- 契约新增错误码 `SERVICE_UNAVAILABLE`（503）：Contracts、`docs/contracts.md`、设计书第 8 节错误表已同步；SDK 已有这个码。
- 部署说明在 `backend/deploy/README.md`。
