# 后端（backend）

字段可选 `source` 分为 `input` 和 `template`，分别只取接口或模板提供的数据；模板提供的 `auto` 与 `default` 互斥且不进入调用样例，类型检查仍保留。省略来源的旧模板保持接口优先回退规则，见 ADR 0016 和 `docs/contracts.md`。

标签生成服务的服务端：模板 JSON 契约、渲染引擎、按领域驱动设计分层的 ASP.NET Core 服务（ADR 0010）。解决方案 `LabelService.Backend.slnx`，.NET 10。

```
backend/
├─ LabelService.Backend.slnx
├─ Directory.Build.props          后端全部项目的编译设置
├─ Directory.Packages.props       后端的 NuGet 版本
├─ src/
│  ├─ LabelService.Contracts/       契约：模板 JSON 模型、错误码、HTTP 头（零依赖）
│  ├─ LabelService.Rendering/       渲染引擎：绑定、校验、排版、绘制、输出
│  ├─ LabelService.Domain/          领域层：实体、领域规则、仓储接口
│  ├─ LabelService.Application/     应用层：应用服务、Dto、内存缓存、开放接口的用例
│  ├─ LabelService.Infrastructure/  基础设施层：EF Core 和三种数据库、仓储、文件、令牌、后台任务
│  └─ LabelService.Api/             接口层：路由、鉴权、指标、组装（启动项目）
├─ tests/
│  ├─ LabelService.Backend.Checks/  检查程序（含跨前端、调用端的契约一致性检查和分层规则）
│  ├─ LabelService.Benchmarks/      渲染基准和基准门禁（baseline.json）
│  └─ load/                         k6 压测脚本、压测报告模板
├─ tools/add-migration.ps1       给三种数据库各生成一次迁移
├─ assets/fonts/                 开发和检查用的思源黑体、思源宋体（文件不提交，见那里的 README；发布包不带字体，正式环境在管理后台上传，ADR 0011）
├─ assets/icons/                 GB/T 191 图标（SVG，和设计器同一套）
├─ assets/images/                内置图片素材（模板里写 asset:文件名）
├─ deploy/                       Dockerfile、部署说明、监控样例（monitoring/）
└─ samples/
   ├─ templates/                  示例模板，开发环境首次建库时导入为已发布版本
   ├─ layouts/                    示例拼版版式（A4_100x60_2x4），开发环境版式表为空时导入
   └─ requests/                   示例请求体（UTF-8）
```

各层放什么、一次请求怎么走、新增功能改哪些文件，见 `docs/architecture.md` 的“后端分层”和“扩展点”。

## 常用命令

在仓库根目录执行：

```powershell
dotnet build backend/LabelService.Backend.slnx
dotnet format backend/LabelService.Backend.slnx --verify-no-changes --no-restore
dotnet run --project backend/tests/LabelService.Backend.Checks

# 启动服务：http://localhost:5080，开发 Key lbl_test_local_dev
dotnet run --project backend/src/LabelService.Api

# 生成一张箱标
curl.exe -o box.png -H "X-Api-Key: lbl_test_local_dev" -H "Content-Type: application/json" --data-binary "@backend/samples/requests/box-label.render.json" http://localhost:5080/api/v1/render

# 只检查注释（列出全部缺漏，不在第一处停下）
dotnet run --project backend/tests/LabelService.Backend.Checks -- docs

# 渲染基准（必须 Release）
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --filter "*"
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --gate     # 基准门禁，见 docs/performance.md
```

## 要点

- 依赖方向：Contracts ← Domain ← Application ← Infrastructure ← Api，Rendering 只给 Application 及以上用。检查程序按项目引用和 `using` 核对。Contracts 是契约，改动规则见根目录 AGENTS.md 的“契约变更”。
- 命名：实体 `XxxEntity`、仓储 `IXxxRepository`、应用服务 `IXxxAppService`、输出 `XxxDto`、输入 `CreateXxxDto` / `UpdateXxxDto` / `SaveXxxDto` / `XxxInput`、路由 `XxxEndpoints`、配置 `XxxOptions`。
- 依赖注入用 .NET 自带的容器：`AddLabelApplication`、`AddLabelInfrastructure`、`AddLabelApi` 三个注册入口，`Program` 只调这三个。
- 渲染是热路径：不访问数据库、磁盘、网络，单张 P95 ≤ 100 ms，见 `docs/performance.md`。
- 单实例部署（ADR 0003）。部署、备份与恢复见 `deploy/README.md`（ADR 0008）。
- 文本渲染需要字体：先按 `assets/fonts/README.md` 把思源黑体（或同款的 Noto Sans SC）放进 `assets/fonts/`，否则文本检查不通过、中文显示成方框。
- 后端检查程序会按文本读取 `client/` 和 `frontend/` 的源码，核对错误码、HTTP 头、元素类型、示例模板是否一致。改了契约，三个部分要一起改。
- 注释（AGENTS.md R5）：产品代码、检查程序、基准程序的每个类型和成员（包括私有的）都写完整的 XML 注释，检查程序用反射加 XML 文档逐个核对（`DocumentationChecks`）；`[GeneratedRegex]` 的声明按文本核对。`tests/` 下的项目也输出 XML 文档（`tests/Directory.Build.props`），基准程序按 Debug 编译结果核对，要先编译解决方案。

## 数据库

- **种类**：配置 `LabelService:Data:Provider`，`Sqlite`（默认）、`SqlServer`、`PostgreSql`。SQLite 的库文件在数据目录里；另外两种配 `LabelService:Data:ConnectionString`，调用日志可以用单独的 `LogConnectionString`（不配时和主库同一个库，表名不冲突，日志库的迁移记录在 `__ef_migrations_history_log`）。连接字符串不要写进仓库，用环境变量或部署机器上的配置。

  ```json
  "LabelService": {
    "Data": {
      "Provider": "SqlServer",
      "ConnectionString": "Server=db01;Database=LabelService;User Id=label;Password=…;TrustServerCertificate=true"
    }
  }
  ```

- **数据目录**：配置 `LabelService:Data:Directory`，默认程序目录下的 `data/`（本地 `dotnet run` 时在 `backend/src/LabelService.Api/bin/Debug/net10.0/data/`）。不管用哪种数据库都要有：上传的素材（`assets/`）、上传的字体（`fonts/`，ADR 0011）、公司 Logo（`branding/`，系统信息页上传）、JWT 签名密钥放在这里；SQLite 时还有 `label.db`、`label-log.db`。
- **升级**：启动时自动建库、执行迁移。SQLite：每次初始化或升级（含没有迁移记录的库）均先快照到 `backups/`，失败自动恢复（`Infrastructure/Persistence/DatabaseUpgrade.cs`），一期早期的旧库把模板分类和调用方授权换算成业务分组（`Persistence/Sqlite/LegacyUpgrade.cs`）。SQL Server、PostgreSQL：每个迁移在事务里执行，失败自动回滚、服务不启动。
- **改表结构**：改领域层的实体、`Infrastructure/Persistence/Configurations/` 后，给三种数据库各生成一次迁移，不要手改迁移文件和快照。检查程序会核对六套迁移（主库、日志库各三种）都和模型一致。

  ```powershell
  dotnet tool install --global dotnet-ef        # 第一次用
  powershell -NoProfile -ExecutionPolicy Bypass -File .\backend\tools\add-migration.ps1 -Name 迁移名（英文，如 AddStock）                  # 主库
  powershell -NoProfile -ExecutionPolicy Bypass -File .\backend\tools\add-migration.ps1 -Name 迁移名（英文，如 AddStock） -Database Log    # 日志库
  ```

- **在真实的 SQL Server、PostgreSQL 上检查**：设置环境变量 `LABEL_TEST_SQLSERVER`、`LABEL_TEST_POSTGRES`（不带库名的连接字符串，如 `Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true`）后运行后端检查程序，会建一个临时库跑一遍主要流程（登录、业务分组、模板发布、出图、调用日志、概览），跑完删库。

## 模板和调用方

- **示例模板**：开发环境首次建库（库里没有模板）时，把 `samples/templates` 导入为已发布版本 1，走正常的保存草稿和发布流程。改了示例模板想重新导入，停掉服务、删掉数据目录（或数据库）再启动。
- **拼版版式**：表 `sheet_layout`，管理接口 `/api/admin/layouts` 维护，保存后立即更新内存缓存 `Application/Layouts/SheetLayoutCache.cs`（开放接口只读它）。开发环境版式表为空时导入 `samples/layouts`；模板来源是 `Files` 时也从这个目录读。
- **模板来源**：配置 `LabelService:Templates:Source`，默认 `Database`；设成 `Files` 时直接读示例目录、不建库，只适合只读演示。
- **草稿和发布**：核心流程在 `Application/Templates/TemplateManager.cs`（保存草稿、发布、放弃草稿、回滚到旧版本），管理接口经 `TemplateAppService` 调它。同一模板最多一个草稿，草稿的版本号是“最新发布版本 + 1”；发布前做静态检查并用样例值试渲染计时，通过后立即生效；已发布版本不能修改、不能删除。
- **开放接口**只读 `Application/Templates/PublishedTemplateCache.cs` 的内存缓存（启动时整体加载，发布时追加），不访问数据库。
- **调用方**：数据库 `api_client` 表（Key 只存 SHA-256）加上配置文件 `LabelService:Auth:Clients`，启动时加载进内存（`Application/ApiClients/ApiKeyAuthenticator.cs`）；新建、改权限、启停、重置 Key 后立即生效。新 Key 正式环境前缀 `lbl_live_`，其他环境 `lbl_test_`。
- **调用日志**：日志库的 `render_log` 表（配置 `LabelService:RenderLog`：队列上限、每批条数、保留天数，默认 180 天）。请求结束时投递到队列，后台服务按批写入，队列满丢弃并计入指标 `label_log_dropped_total`，写入失败只记警告。
- **管理接口**：`/api/admin`，接口清单见 `docs/admin-api.md`，登录和权限见 ADR 0006。首次启动建 `admin` 账号（开发环境密码 `admin123`，首次登录必须改）。配置 `LabelService:Admin:SigningKey`（Base64，≥ 32 字节）指定 JWT 签名密钥，不配置时生成在数据目录的 `admin-signing.key`。

隔离功能整合补充：日志命名空间源码必须随 Git 纳入；源码检查会拦截宽泛的 `logs/` 排除。真实数据库回归给主库和日志库配置同一个临时数据库，避免访问其他日志库。SQLite 初始化失败后恢复迁移前快照，并拒绝启动；完整回归包含无迁移历史和首次空库场景。
