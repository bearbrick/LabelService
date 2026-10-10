# 部署

标签服务单实例部署（ADR 0003），可以跑在 Docker（Linux）里，也可以装成 Windows 服务（ADR 0008）。两种方式的配置、数据目录、备份都一样。

## 两条硬规定

- **数据目录不能放网络共享盘**（SMB、NFS）。SQLite 的文件锁在网络盘上不可靠，会损坏数据库。
- **同一个数据目录只能有一个服务进程**。不要为了“高可用”起两个进程指向同一个目录；需要多实例时先换 SQL Server（ADR 0003）。

## 数据目录里有什么

| 文件 | 内容 | 能不能删 |
| --- | --- | --- |
| `label.db`（及 `-wal`、`-shm`） | 模板、版本、业务分组、用户、角色、调用方、画布预设、素材登记 | 不能 |
| `label-log.db`（及 `-wal`、`-shm`） | 调用日志、操作日志，保留 180 天 | 删了只丢日志 |
| `assets/` | 上传的图片素材 | 不能 |
| `admin-signing.key` | 管理后台 JWT 签名密钥（没配置 `LabelService:Admin:SigningKey` 时自动生成） | 删了所有人要重新登录 |
| `backups/` | 每日备份快照；升级数据库前的快照 `label-before-upgrade-*.db` | 可以按需清理（升级确认没问题后再删升级前的快照） |

## 常用配置

配置写在 `appsettings.json` 或环境变量里（环境变量把 `:` 换成 `__`，如 `LabelService__Data__Directory`）。

| 配置 | 默认值 | 说明 |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://localhost:5000` | 监听地址；Docker 镜像里是 `http://+:8080` |
| `LabelService:Data:Directory` | `data`（程序目录下） | 数据目录 |
| `LabelService:Admin:SigningKey` | 空 | JWT 签名密钥（Base64，至少 32 字节）；空时自动生成在数据目录 |
| `LabelService:Admin:InitialPassword` | 空 | 首次启动时 `admin` 的初始密码；空时随机生成并写在启动日志里 |
| `LabelService:Render:MaxConcurrency` | `0`（= CPU 核数） | 同时渲染的请求数，多出的排队 |
| `LabelService:Render:QueueTimeoutMs` | `2000` | 排队超过这个时间返回 503 `SERVICE_UNAVAILABLE`（带 `Retry-After: 1`，SDK 会自动重试） |
| `LabelService:Warmup:Enabled` | `true` | 启动时用样例值把每个已发布模板按 PNG、PDF、ZPL 各渲染一次 |
| `LabelService:Warmup:BudgetSeconds` | `25` | 预热最多花多久，超过就停下并标记就绪 |
| `LabelService:Backup:Enabled` | `true` | 每天自动备份 |
| `LabelService:Backup:Time` | `02:30` | 每天几点备份（服务器本地时间） |
| `LabelService:Backup:Keep` | `7` | 保留最近几份 |
| `LabelService:Backup:Directory` | `backups`（数据目录下） | 备份目录，建议改到另一块盘（绝对路径） |
| `LabelService:Backup:IncludeLogs` | `true` | 是否连调用日志库一起备份 |

非开发环境下不加载示例模板，开发 Key `lbl_test_local_dev` 也无效；调用方在管理后台“调用方与密钥”里建。

## 健康检查

- `GET /health`：存活检查，进程能响应就是 200。
- `GET /metrics`：Prometheus 指标，抓取配置、告警规则、Grafana 仪表盘在 `monitoring/`（见那里的 README）。只在内网开放。
- `GET /health/ready`：就绪检查，启动预热完成前返回 503，完成后 200（响应里写了预热了几个模板、用了多久）。负载均衡、部署脚本等它变成 200 再放流量。本机测试示例箱标从进程启动到就绪约 1.5 秒。

## Docker（Linux）

镜像里只有思源黑体、思源宋体两个字体，不依赖任何系统字体（SkiaSharp 用不依赖 fontconfig 的原生库）。

```bash
# 在 backend/ 目录。构建前先按 assets/fonts/README.md 放好字体文件。
docker build -f deploy/Dockerfile -t label-service .
docker volume create label-data
docker run -d --name label-service -p 8080:8080 -v label-data:/data \
  -e LabelService__Admin__InitialPassword='首次登录后要改的密码' \
  --restart unless-stopped label-service
docker logs label-service          # 看启动日志、预热结果
curl http://localhost:8080/health/ready
```

- 容器以 `app` 用户运行；用绑定挂载（`-v /srv/label:/data`）时，宿主目录要给 UID 1654 写权限。
- 不要用 `--scale` 或多个容器挂同一个卷（见上面的硬规定）。

## Windows 服务

```powershell
# 在仓库根目录发布（需要 .NET 10 SDK）。指定 -r 时会预编译（ReadyToRun）。
dotnet publish backend/src/LabelService.Server -c Release -r win-x64 --self-contained false -o D:\LabelService\app

# 用管理员 PowerShell 安装服务（数据目录放本机磁盘）。
New-Service -Name LabelService -DisplayName "标签服务" -StartupType Automatic `
  -BinaryPathName '"C:\Program Files\dotnet\dotnet.exe" "D:\LabelService\app\LabelService.Server.dll"'
[Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://+:8080', 'Machine')
[Environment]::SetEnvironmentVariable('LabelService__Data__Directory', 'D:\LabelService\data', 'Machine')
Start-Service LabelService
Invoke-WebRequest http://localhost:8080/health/ready -UseBasicParsing
```

- 服务名是 `LabelService`（程序里 `UseWindowsService` 用的名字）。日志写到 Windows 事件查看器的“应用程序”日志。
- 也可以在 `D:\LabelService\app\appsettings.json` 里写配置，不用机器级环境变量。
- 升级：`Stop-Service LabelService`，覆盖 `app` 目录（不要动 `data`），`Start-Service LabelService`。数据库迁移在启动时自动执行：有待执行的迁移时先把库快照到 `data/backups/label-before-upgrade-时间.db`（日志库同理）；迁移或数据转换中途失败时自动用这份快照恢复，服务不启动，启动日志写明原因和快照路径，排除原因后再启动会重新升级。T-014 以前的旧库（模板分类、调用方允许的模板编码）在升级时换算成业务分组，授权不变（T-021）。不要用 `dotnet ef database update` 升级旧库：它不备份，也不做这一步换算。

## 前端（设计器和管理后台）

`frontend/` 用 `npm ci; npm run build` 打包到 `frontend/dist/`，由 Nginx 或 IIS 托管静态文件，并把 `/api`、`/fonts` 转发给标签服务（和开发时 Vite 的代理一样）。

```nginx
location / { root /srv/label-frontend; try_files $uri /index.html; }
location /api/   { proxy_pass http://127.0.0.1:8080; client_max_body_size 10m; }
location /fonts/ { proxy_pass http://127.0.0.1:8080; }
location /health { proxy_pass http://127.0.0.1:8080; }
# /metrics 不转发：只给内网的 Prometheus 直接抓取。
```

## 备份与恢复

服务每天在 `LabelService:Backup:Time` 用 SQLite 的 `VACUUM INTO` 生成快照：`backups/yyyyMMdd-HHmmss/` 里有 `label.db`、`label-log.db`（可关）和 `assets/`，只保留最近 `Keep` 份。快照是完整、一致的库文件，服务运行时生成也不影响使用。**不要直接复制正在运行的 .db 文件当备份**（可能不一致）。

恢复步骤（检查程序 `HostingChecks` 每次都会演练一遍，见下）：

1. 停服务：`docker stop label-service` 或 `Stop-Service LabelService`。
2. 把数据目录里的 `label.db` 改名留底（如 `label.db.bak`），删掉 `label.db-wal`、`label.db-shm`。
3. 把快照里的 `label.db` 复制到数据目录；要恢复日志就同样处理 `label-log.db`；把快照的 `assets/` 复制回数据目录的 `assets/`。
4. 启动服务，等 `/health/ready` 变成 200，登录管理后台确认模板和调用方都在，用接口调试试生成一张。

演练记录：后端检查程序的 `HostingChecks.CheckRestoreAsync` 按上面的步骤做——生成快照，删掉画布预设，停服务，用快照替换 `label.db`、删掉 `-wal`、`-shm`，重启后数据回来、示例模板照常生成；另外验证了换进来的库文件不是 WAL 模式时服务也能启动。

## 还没验证的

- Docker 镜像没有在本机构建过（开发机上没有 Docker）；`dotnet publish -r linux-x64` 已验证能出包，原生库（libSkiaSharp、libHarfBuzzSharp、libe_sqlite3）和字体都在包里。第一次在 Linux 上跑时，用示例请求生成一张 PNG 看中文是否正常。
- Windows 服务没有实际安装过（需要管理员权限）。
