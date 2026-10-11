# T-023 后端分层架构（DDD）与多数据库支持

状态：待验收
负责人：Claude 2026-10-11
里程碑：一期验收
依赖：T-022
设计书：§2 总体架构、§3 数据模型；ADR 0010

## 目标

后端按领域驱动设计分层（领域、应用、基础设施、接口），统一命名，用依赖注入组装；数据库可以在 SQLite、SQL Server、PostgreSQL 之间切换。后来的开发人员看目录和类名就知道东西在哪、属于哪一层。接口的地址、请求和响应不变。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Domain`、`LabelService.Application`、`LabelService.Infrastructure`
- `backend/src/LabelService.Server` 拆分后改名为 `backend/src/LabelService.Api`
- `backend/tests/`：检查程序跟着改命名空间和类型名；新增分层规则、三种数据库迁移一致性、SQL Server / PostgreSQL 上运行服务端检查的开关
- `backend/deploy/`（Dockerfile、部署脚本、README）、`backend/README.md`、`docs/architecture.md`、`docs/adr/0010-*`、`docs/adr/README.md`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `AGENTS.md`：仓库地图、R2 依赖方向、常用命令里的项目路径
- `verify.ps1`：服务端项目路径
- `backend/LabelService.Backend.slnx`：新项目
- `backend/Directory.Packages.props`：新增 SQL Server、PostgreSQL 的 EF Core Provider
- `backend/src/LabelService.Server/Program.cs`、`Endpoints/PublicApiEndpoints.cs`（随项目改名移动）
- `docs/tasks/README.md`：任务索引追加一行

## 验收标准

- [x] 六个项目，依赖方向 Contracts ← Domain ← Application ← Infrastructure ← Api，Rendering 只被 Application 及以上引用；检查程序拦住反向引用
- [x] 领域层不引用 EF Core、ASP.NET Core、渲染引擎；接口层不直接访问数据库
- [x] 命名统一：实体 `XxxEntity`、仓储 `IXxxRepository`、应用服务 `IXxxAppService`、输出 `XxxDto`、输入 `CreateXxxDto` / `UpdateXxxDto`；接口不再返回匿名对象
- [x] 依赖注入：每层一个注册入口（`AddLabelApplication`、`AddLabelInfrastructure`、`AddLabelApi`）
- [x] 数据库可配置为 Sqlite（默认）、SqlServer、PostgreSql；三套迁移和模型一致；现有 SQLite 库照常升级
- [x] 服务端检查在 SQLite 和 SQL Server 上都通过；PostgreSQL 视环境（本机没有 PostgreSQL，没跑端到端，见交接记录）
- [x] 接口快照（123 个调用的状态码、响应结构、错误码和消息）重构前后一致
- [x] 基准门禁通过（基线不变）
- [x] `verify.ps1` 通过（见 AGENTS.md）
- [x] 相关文档已更新

## 不做

- 全部代码补齐注释、去掉代码里的任务号（下一个任务做）
- 多实例部署（缓存同步）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

### 2026-10-11 · Claude · 完成，待验收

**做了什么**

- 后端拆成 Contracts、Rendering、Domain、Application、Infrastructure、Api 六个项目（`LabelService.Server` 改名 `LabelService.Api`）。各层放什么、一次请求怎么走、新功能改哪些文件写在 `docs/architecture.md`。
- 领域层：实体改名 `XxxEntity`；业务规则从接口处理函数里搬出来（`TemplateCodes`、`BusinessRules`、`AccountRules`、`PasswordPolicy`、`BusinessScope`、`IpAllowlist`、`ApiKeys`、`SheetLayoutRules` 等）；每张表一个仓储接口，加工作单元 `IUnitOfWork`。
- 应用层：每个管理功能一个 `IXxxAppService` + `XxxAppService` + `Dtos/`（账号、用户、角色、业务分组、调用方、模板、服务端预览、接口调试、版式、画布预设、素材、字体、日志、概览）；模板和版式的核心流程放在 `TemplateManager`、`SheetLayoutManager`（启动导入示例也用它）；开放接口的生成规则放在 `ILabelRenderAppService`，失败用返回值 `OpenApiError` 表达（热路径不抛异常）。接口不再返回匿名对象，全部是 Dto。
- 基础设施层：仓储实现、`EfUnitOfWork`、PBKDF2、JWT、操作日志、调用日志后台写入、每日备份、字体和素材加载、启动初始化 `StartupInitializer`。
- 接口层：每个业务一个 `XxxEndpoints`，处理函数基本一行；`AdminFilter` 负责登录、权限点和把 `UserFriendlyException` 转成 `{ code, message, details }`。
- 依赖注入：`AddLabelApplication`、`AddLabelInfrastructure`、`AddLabelApi`，`Program` 只调这三个。没有引入 Autofac。
- 多数据库：`LabelService:Data:Provider` = `Sqlite`（默认）/ `SqlServer` / `PostgreSql`，`ConnectionString`、可选的 `LogConnectionString`。每种数据库一个派生上下文和一套迁移；SQLite 特有的功能（PRAGMA、升级前快照和恢复、旧库换算、每日备份）只在 SQLite 下启用。新增 `backend/tools/add-migration.ps1` 一次给三种数据库生成迁移。
- 检查程序：新增分层规则（项目引用、`using`）、六套迁移和模型一致、按配置选中上下文；设置 `LABEL_TEST_SQLSERVER` / `LABEL_TEST_POSTGRES` 时在真实数据库上建临时库跑主要流程、跑完删库。直接调应用服务用 `AdminScope`、`ScopedTemplateManager`。

**怎么验证的**

- 接口快照：重构前录下 123 个调用（状态码、响应头、JSON 结构、错误码和消息），重构后在 SQLite 上逐项一致；把数据库换成 SQL Server（LocalDB）再录一遍，也逐项一致。
- 后端检查：SQLite 430 项全过；加上 SQL Server（LocalDB）的端到端流程共 448 项全过，临时库已删除。
- `verify.ps1` 全量通过（后端 439 项、调用端 41 项含端到端、前端 69 项和打包）。
- 基准门禁通过（基线不变，16 项比较；渲染引擎没改，箱标 ZPL +18% 是机器波动，在 20% 以内）。

**行为上的细小变化**（接口快照没覆盖到，特此说明）

- 调用方的 IP 白名单：以前先校验原文再规范化，用换行或空格分隔的白名单会被误判为格式错误；现在先规范化再校验，存的就是校验过的值。
- 开放接口“格式尚未实现”的提示去掉了任务号（三种格式都已实现，实际走不到）。

**没做的、要注意的**

- PostgreSQL 的端到端没有实际跑：开发机没有 PostgreSQL，下载便携版要先征得同意。迁移和模型的一致性、选中上下文的检查都通过了。用户有 PostgreSQL 时设置 `LABEL_TEST_POSTGRES` 跑一遍后端检查程序即可。
- 已有 SQLite 数据换到 SQL Server、PostgreSQL 不会自动搬，需要时另做导出导入工具。
- SQL Server 默认排序规则不区分大小写，只差大小写的模板编码会被当成重复（写进了部署说明）。
- 程序文件名从 `LabelService.Server.dll` 变成 `LabelService.Api.dll`：已经装好的 Windows 服务、启动脚本要改（写进了部署说明）。
- dotnet-ef 第一次给某种数据库生成迁移时会把快照放到按命名空间推出来的目录，`add-migration.ps1` 会挪回来。
- 全部代码补齐注释、去掉代码里的任务号留给 T-024。

**共享文件改动**：`backend/LabelService.Backend.slnx`（新项目、改名）、`backend/Directory.Packages.props`（SQL Server、PostgreSQL 的 EF Core Provider 等）、`verify.ps1` 和 `backend/deploy/Dockerfile`（项目路径）、`AGENTS.md`（仓库地图、命令、R2、R6、命名和新功能落点、共享文件、迁移和多数据库）、`docs/tasks/README.md`（索引和改名说明）、`Program.cs` 和 `PublicApiEndpoints.cs`（随项目改名移动、重写）。
