# 架构说明

标签生成服务把“模板 + 数据”变成可直接打印的文件（PNG、PDF、ZPL）。业务系统通过开放接口或客户端 SDK 调用；网页设计器负责画模板。完整背景见[设计书](design/label-service-design.html)。

## 三个部分

仓库分成三个互不引用的部分，只通过 HTTP 和模板 JSON 交互（[ADR 0004](adr/0004-three-parts.md)）：

```
  frontend/（Vue 设计器、管理后台）           client/（SDK、WinForms 演示程序）
        │ HTTP + 模板 JSON                          │ HTTP（不引用后端程序集）
        └──────────────────┬────────────────────────┘
                           ▼
  backend/ ┌──────────────────────────────────────────────────────────────┐
           │ LabelService.Api             接口层：路由、鉴权、指标、组装        │
           ├──────────────────────────────────────────────────────────────┤
           │ LabelService.Infrastructure  基础设施层：三种数据库、仓储、文件     │
           ├──────────────────────────────────────────────────────────────┤
           │ LabelService.Application     应用层：应用服务、Dto、内存缓存       │
           ├─────────────────────────────┬────────────────────────────────┤
           │ LabelService.Domain  领域层  │ LabelService.Rendering  渲染引擎   │
           ├─────────────────────────────┴────────────────────────────────┤
           │ LabelService.Contracts       模板 JSON 模型、协议常量（契约）      │
           └──────────────────────────────────────────────────────────────┘
```

| 部分 | 解决方案 / 入口 | 编译配置 | 检查 |
| --- | --- | --- | --- |
| `frontend/` | `package.json` | `tsconfig.json`、`vite.config.ts` | `npm run check`（检查程序）、`npm run build`（vue-tsc 类型检查） |
| `backend/` | `LabelService.Backend.slnx` | `backend/Directory.Build.props`、`Directory.Packages.props` | `backend/tests/LabelService.Backend.Checks` |
| `client/` | `LabelService.Client.slnx` | `client/Directory.Build.props`、`Directory.Packages.props` | `client/tests/LabelService.Client.Checks` |

- 契约的权威定义在后端 Contracts。调用端有错误码和 HTTP 头的副本（[ADR 0002](adr/0002-client-sdk-standalone.md)），前端有模板模型和示例模板的副本。
- **跨部分的一致性由后端检查程序负责**：它按文本读取 `client/` 和 `frontend/` 的源码，核对错误码、HTTP 头、元素类型、示例模板；同时检查三个部分之间没有项目引用或跨文件夹 import。
- SDK 和服务端是否真正对得上，由调用端检查程序的端到端用例验证；根目录 `verify.ps1` 全量运行时会临时启动后端服务执行它们。

## 后端分层

后端按领域驱动设计分层（[ADR 0010](adr/0010-layered-architecture-and-databases.md)），依赖只能从上往下，检查程序按项目引用和 `using` 核对：

| 层 | 项目 | 放什么 | 不能做什么 |
| --- | --- | --- | --- |
| 契约 | `LabelService.Contracts` | 模板 JSON、开放接口的请求和响应、错误码、HTTP 头、计时阶段 | 不引用任何项目和包 |
| 渲染引擎 | `LabelService.Rendering` | 绑定、校验、排版、绘制、编码 | 只引用 Contracts；不访问数据库和网络 |
| 领域层 | `LabelService.Domain` | 实体 `XxxEntity`、领域规则（`XxxRules`、`PasswordPolicy`、`BusinessScope` 等）、仓储接口 `IXxxRepository`、工作单元 `IUnitOfWork`、`UserFriendlyException` | 不用 EF Core、ASP.NET Core、渲染引擎，不引用任何包 |
| 应用层 | `LabelService.Application` | 应用服务 `IXxxAppService`（用例：业务范围检查、操作日志、转换 Dto）、输入输出模型 `XxxDto`、核心流程（`TemplateManager`、`SheetLayoutManager`）、内存缓存、开放接口的用例 `ILabelRenderAppService`、对基础设施的抽象（`IPasswordHasher`、`IAdminTokenService`、`IOperationLogger`、`IAssetFileStore`、`ICurrentAdmin`） | 不用 EF Core 和 ASP.NET Core |
| 基础设施层 | `LabelService.Infrastructure` | EF Core 的上下文和三种数据库的迁移、仓储实现、工作单元、PBKDF2 密码哈希、JWT、操作日志和调用日志的写入、每日备份、字体和素材的加载、启动初始化 | — |
| 接口层 | `LabelService.Api` | 路由 `XxxEndpoints`（一般一行：调应用服务）、登录检查 `AdminFilter`、当前用户 `HttpCurrentAdmin`、错误响应、`/metrics`、`/health`、`Program` | 不直接访问数据库 |

依赖注入用 .NET 自带的容器，每层一个注册入口：`AddLabelApplication`、`AddLabelInfrastructure`、`AddLabelApi`，`Program` 只调用这三个。生命周期：内存缓存、渲染相关、开放接口的服务是单例；管理后台的应用服务、仓储、工作单元每个请求一个，共用同一个数据库上下文，一次操作的修改由工作单元一起保存。

**一次管理操作的流程**（以“发布模板”为例）：`TemplateEndpoints` 收到 `POST /api/admin/templates/{code}/publish` → `AdminFilter` 核对登录和权限点 `tpl.publish`，把当前用户交给 `HttpCurrentAdmin` → `TemplateAppService.PublishAsync` 检查业务范围 → `TemplateManager.PublishAsync` 取草稿、发布前检查、改实体、`IUnitOfWork.SaveChangesAsync` 写库、更新内存缓存 → 应用服务记操作日志、返回 `PublishResultDto` → 接口层写 JSON。用户能修正的问题抛 `UserFriendlyException`（带错误码），`AdminFilter` 转成 `{ code, message, details }` 和对应的 HTTP 状态码。

## 单张生成的处理流程

`POST /api/v1/render` 的每一步都单独计时，阶段名就是 Server-Timing 里的名字。下表路径相对于 `backend/src/`。整个流程只用内存（模板、版式、调用方都在应用层的缓存里），不访问数据库和磁盘（AGENTS.md R3）。

| 阶段 | 做什么 | 代码位置 |
| --- | --- | --- |
| `auth` | 按 X-Api-Key 的 SHA-256 在内存里查调用方，检查 IP 白名单 | `LabelService.Application/ApiClients/ApiKeyAuthenticator.cs`（由 `PublicApiEndpoints` 调用） |
| `validate` | 读请求体，检查 format、dpi、copies、拼版参数 | `LabelService.Api/Endpoints/OpenApi/PublicApiEndpoints.cs` → `LabelService.Application/OpenApi/LabelRenderAppService.cs` |
| `tpl` | 从内存取已发布模板，检查调用方能否使用；取版式 | `LabelService.Application/Templates/PublishedTemplateCache.cs`、`Layouts/SheetLayoutCache.cs` |
| `validate` | 按字段定义校验数据、转换类型 | `LabelService.Rendering/Validation/FieldValidator.cs` |
| `layout` | 替换绑定、文本测量、条码编码 | `LabelService.Rendering/LabelRenderer.cs` → 各 `IElementPainter.Layout` |
| `draw` | 按元素顺序画到画布，统一处理旋转 | 各 `IElementPainter.Draw` |
| `encode` | 编码成 PNG / PDF / ZPL | `LabelService.Rendering/Output/*OutputDocument.cs` |
| `total` | 写响应头、记指标和日志、投递调用日志 | `LabelService.Api/Endpoints/OpenApi/RenderCall.cs` |

开放接口的失败是常态（调用方传错数据），用返回值 `OpenApiError` 表达，不抛异常；接口层统一用 `ErrorResponse` 返回（`LabelService.Api/Endpoints/OpenApi/ApiErrors.cs`），HTTP 状态码由 `ErrorCodes.StatusCodes` 决定。

## 目录职责

**后端（`backend/`）**

| 路径 | 职责 |
| --- | --- |
| `src/LabelService.Contracts/Templates/` | 模板 JSON 模型：模板、纸张、字段、7 种元素、样式和枚举 |
| `src/LabelService.Contracts/Layouts/` | 拼版版式 JSON 模型（ADR 0005）：纸张、格子尺寸、格子位置和旋转、打印偏移、裁切线 |
| `src/LabelService.Contracts/Protocol/` | 错误码、HTTP 头、计时阶段、取值上限、请求和响应体 |
| `src/LabelService.Contracts/LabelJson.cs`、`LabelUnits.cs` | 统一的 JSON 设置；mm、pt、点的换算 |
| `src/LabelService.Rendering/Binding/` | `{{key:format}}` 绑定替换 |
| `src/LabelService.Rendering/Validation/` | 字段校验和类型转换 |
| `src/LabelService.Rendering/Elements/` | 每种元素一个绘制器，`ElementPainters.cs` 登记；文本排版（换行、自动缩小、对齐）在 `TextLayout.cs`；条码编码在 `LinearSymbols.cs`（一维）、`MatrixSymbols.cs`（二维），编码结果缓存在 `Symbols.cs`（一维、二维共用） |
| `src/LabelService.Rendering/Assets/` | 素材库：图标、图片素材启动时加载，按目标尺寸栅格化后缓存；SVG 清洗（`SvgSanitizer`）；Floyd–Steinberg 抖动 |
| `src/LabelService.Rendering/Fonts/` | 字体注册表：启动时加载字体目录，按模板字体名匹配，缺失时降级；缓存字体和 SKFont |
| `src/LabelService.Rendering/Output/` | 每种格式一个输出文档，`OutputDocuments.cs` 登记 |
| `src/LabelService.Rendering/Sheets/` | 拼版：`SheetRenderer` 按版式的格子顺序把标签排到纸上（平移、旋转、裁剪，一页排满换页，打印偏移、裁切线），PDF 一页一张纸、PNG 多页打包 ZIP；`LabelPlan` 把不引用字段的元素排版一次、录成画面，各格子回放 |
| `src/LabelService.Rendering/Diagnostics/` | 分阶段计时、Server-Timing 生成 |
| `src/LabelService.Domain/` | 按业务分文件夹：`Templates/`（模板、版本、编码规则、仓储接口）、`Businesses/`、`ApiClients/`（Key 生成和哈希、IP 白名单）、`Identity/`（用户、角色、权限点、内置角色、密码和账号规则、业务范围）、`Layouts/`（版式实体、检查和换算）、`Resources/`（画布预设、素材）、`Logs/`（调用日志、操作日志）、`Shared/`（工作单元、ID 列表、`UserFriendlyException`） |
| `src/LabelService.Application/` | 按业务分文件夹，每个文件夹里是 `IXxxAppService`、`XxxAppService` 和 `Dtos/`：`Identity/`（账号、用户、角色）、`Businesses/`、`ApiClients/`（含 Key 鉴权的内存快照）、`Templates/`（模板管理、核心流程 `TemplateManager`、发布前检查、缩略图、服务端预览、已发布模板的内存缓存）、`Layouts/`（版式管理、`SheetLayoutManager`、版式缓存、测试页）、`Resources/`（画布预设、素材、字体）、`Logs/`（日志查询、调用日志队列）、`Dashboard/`、`ApiTest/`、`OpenApi/`（开放接口的用例）、`Rendering/`（并发限制）、`Diagnostics/`（指标）、`Hosting/`（启动预热）；`ApplicationServiceCollectionExtensions.cs` 登记 |
| `src/LabelService.Infrastructure/Persistence/` | EF Core：通用的 `LabelDbContext`（主库）、`LogDbContext`（调用日志库）和 `Configurations/`（每张表一个），每种数据库一个派生上下文和一套迁移（`Sqlite/`、`SqlServer/`、`PostgreSql/` 下的 `Migrations/Main`、`Migrations/Log`）；SQLite 的连接 PRAGMA、升级前快照和失败恢复、旧库换算；`DataOptions`（`LabelService:Data`） |
| `src/LabelService.Infrastructure/Repositories/` | 仓储实现和工作单元 `EfUnitOfWork` |
| `src/LabelService.Infrastructure/Security/` | PBKDF2 密码哈希、JWT 登录令牌（签名密钥没配置时生成在数据目录） |
| `src/LabelService.Infrastructure/Logging/` | 操作日志的写入、调用日志的后台批量写入和定期清理 |
| `src/LabelService.Infrastructure/Storage/` | 上传素材的文件存放、示例模板和版式的读取、字体和素材目录的配置 |
| `src/LabelService.Infrastructure/Hosting/` | 启动初始化（迁移、建内置角色和 admin、开发环境导入示例、加载缓存）、每日备份（只支持 SQLite） |
| `src/LabelService.Api/Endpoints/Admin/` | 管理接口 `/api/admin`（`docs/admin-api.md`），每个业务一个 `XxxEndpoints` |
| `src/LabelService.Api/Endpoints/OpenApi/` | 开放接口 `/api/v1`：读请求、调 `ILabelRenderAppService`、请求收尾（计时、指标、调用日志）、错误响应 |
| `src/LabelService.Api/Endpoints/FontFileEndpoints.cs` | `/fonts` 把渲染用的字体提供给设计器 |
| `src/LabelService.Api/Security/` | 登录检查和权限点（`AdminFilter` 每次请求核对用户）、当前用户、管理接口的错误响应 |
| `src/LabelService.Api/Diagnostics/`、`Hosting/` | `/metrics` 的 Prometheus 导出（ADR 0009）；`/health`、`/health/ready` |
| `assets/icons/`、`assets/images/` | GB/T 191 图标（和设计器 `frontend/src/model/icons.ts` 同一套路径，检查程序核对）、内置图片素材；构建时复制到程序目录的 `icons/`、`images/` |
| `assets/fonts/` | 思源黑体、思源宋体，构建时复制到程序目录的 `fonts/`；文件不提交，见那里的 README |
| `deploy/` | Dockerfile、部署说明（Docker、Windows 服务、数据库、备份与恢复，ADR 0008）；`monitoring/` 抓取配置、告警规则、Grafana 仪表盘 |
| `samples/` | 示例模板、示例版式、示例请求体 |
| `tests/LabelService.Backend.Checks/` | 后端检查程序，含跨部分的契约一致性、分层规则和仓库规范检查 |
| `tests/LabelService.Benchmarks/` | BenchmarkDotNet 渲染基准 |

**调用端（`client/`）**

| 路径 | 职责 |
| --- | --- |
| `src/LabelService.Client/` | SDK 公开 API；`Internal/` 放协议副本和请求体生成；`Printing/` 放打印辅助类 |
| `samples/LabelService.Demo.WinForms/` | SDK 演示程序 |
| `tests/LabelService.Client.Checks/` | 调用端检查程序：假服务端用例、协议解析、架构规则、端到端用例 |

**前端（`frontend/`）**

| 路径 | 职责 |
| --- | --- |
| `src/model/` | 模板 JSON 的 TypeScript 镜像、单位换算、元素默认值、绑定预览、模板检查、调用样例、显示格式、图表几何（纯逻辑） |
| `src/editor/` | 画布引擎：视口（`stageViewport.ts`）和编辑（`stage.ts`）、元素绘制（`draw.ts`、`drawText.ts`、`drawCodes.ts`）、吸附、对齐、撤销、条码编码 |
| `src/api/` | 管理接口客户端：带登录令牌的请求封装、和 `docs/admin-api.md` 对应的类型和函数（按业务分文件，`admin.ts` 统一转导出） |
| `src/stores/` | 设计器状态和全部编辑操作（入口 `designer.ts`，按状态、撤销和保存、模板、元素、字段分文件）；登录状态和权限、hash 路由、共用列表、模板工作区（打开、自动保存草稿、发布、版本）、素材 |
| `src/composables/` | 组件用的组合函数：画布上直接编辑文本、拖放到画布 |
| `src/pages/`、`src/components/admin/`、`src/components/workspace/` | 管理后台：登录、工作台、模板库、模板工作区（设计、数据源、接口调试、版本记录）、业务分组、画布预设、素材、调用方、日志、用户、角色、个人设置 |
| `src/components/` | 设计器：工具栏、左侧面板、画布、属性面板、状态栏 |
| `tests/checks.ts`、`tests/docs.ts` | 检查程序，Node 直接运行；`docs.ts` 核对注释规范 |
| `src/samples/` | 示例模板副本（和后端一致，检查程序核对） |

## 扩展点

**新增一个管理功能**（如“标签纸库存”）：

1. 领域层 `Domain/Xxx/`：实体 `XxxEntity`、规则 `XxxRules`、仓储接口 `IXxxRepository`。
2. 基础设施层：`Persistence/Configurations/XxxConfiguration.cs`（表名、长度、索引），在 `LabelDbContext` 里加 `DbSet` 和 `ApplyConfiguration`；`Repositories/XxxRepository.cs`；在 `AddLabelInfrastructure` 登记仓储；给三种数据库各生成一次迁移（命令见 `backend/README.md`）。
3. 应用层 `Application/Xxx/`：`Dtos/XxxDtos.cs`（`XxxDto`、`SaveXxxDto`）、`IXxxAppService`、`XxxAppService`（业务范围、操作日志、调用 `IUnitOfWork` 保存），在 `AddLabelApplication` 登记。
4. 接口层 `Api/Endpoints/Admin/XxxEndpoints.cs`：路由加权限点，在 `AdminEndpoints.MapAdminApi` 登记；同步 `docs/admin-api.md`。
5. 检查程序：接口行为写进对应的 `XxxChecks.cs`（`AdminSession` 调接口），直接调用应用服务用 `AdminScope`。

**新增一种元素的渲染**：在 `backend/src/LabelService.Rendering/Elements/` 新建 `XxxPainter.cs` 实现 `IElementPainter`（`Layout` 做计算，`Draw` 只画），在 `ElementPainters.cs` 登记一行，在后端检查程序的 `RenderChecks.cs` 补像素检查。绘制器必须无状态、线程安全，坐标一律是打印点，旋转已经由 `LabelRenderer` 处理。

**新增一种输出格式**：在 `backend/src/LabelService.Rendering/Output/` 新建 `XxxOutputDocument.cs` 继承 `OutputDocument`，在 `OutputDocuments.cs` 登记。画布坐标必须保持“1 单位 = 1 打印点”，矢量格式自己做缩放。

**模板缓存**：开放接口只通过 `PublishedTemplateCache` 取模板，请求路径上只能查内存。缓存启动时由 `TemplateManager.LoadPublishedAsync` 从数据库整体加载（模板来源是文件时从模板目录加载），发布、换分组、删除时立即更新。

## 状态与部署

- 单实例部署，前期不考虑高可用（[ADR 0003](adr/0003-sqlite-single-instance.md)）。模板、版式、调用方的内存缓存是单进程的，多实例要另做缓存同步，换数据库不改变这一点（ADR 0010）。
- 数据库默认 SQLite：数据目录里有 `label.db`（模板等）、`label-log.db`（调用日志），不能放网络共享盘。配置 `LabelService:Data:Provider` 可换成 SQL Server 或 PostgreSQL（连接字符串 `LabelService:Data:ConnectionString`，调用日志可以另配 `LogConnectionString`）。不管用哪种数据库，数据目录都要有：上传的素材和 JWT 签名密钥放在那里。
- SQLite 特有的功能只在 SQLite 下启用：连接 PRAGMA、升级前快照和失败恢复、旧库换算、每日 `VACUUM INTO` 备份。SQL Server、PostgreSQL 的迁移每个都在事务里执行，备份用数据库自己的工具。
- 开发环境用 `appsettings.Development.json` 里的测试调用方，Key 是 `lbl_test_local_dev`，配置里只存它的 SHA-256。
- 部署（Docker 或 Windows 服务）、数据库配置、预热、并发控制、备份见 `backend/deploy/README.md`（ADR 0008）；监控接入见 `backend/deploy/monitoring/`。
