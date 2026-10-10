# 0010 后端分层（领域驱动设计）与多数据库支持

状态：已采纳（2026-10-11 用户确认）
日期：2026-10-11
关联：T-023；部分取代 0001（后端项目划分）、0003（“只用 SQLite”）

## 背景

一期完成后，用户要求代码按产品标准组织，后来的开发人员能很快上手：

- 有完整的多层架构和领域驱动设计（DDD）的概念，用依赖注入组装；
- 模型按类别命名，一眼看出是实体、输入还是输出；
- 以后能把数据库换成 PostgreSQL、SQL Server，最好现在就支持。

原来后端只有三个项目：契约、渲染引擎、服务。服务（约 9200 行）按功能分文件夹，业务规则写在接口处理函数里，数据库实体直接拿来用，部分接口返回临时拼的匿名对象；数据库只能是 SQLite。

## 决定

### 1. 分层

后端分成六个项目，依赖只能从上往下：

| 项目 | 职责 | 可以引用 |
| --- | --- | --- |
| `LabelService.Contracts` | 对外契约：模板 JSON、开放接口的请求和响应、错误码、HTTP 头 | 无 |
| `LabelService.Rendering` | 渲染引擎：绑定、校验、排版、绘制、PNG / PDF / ZPL 编码 | Contracts |
| `LabelService.Domain` | 领域层：实体、值对象、领域规则、仓储接口、领域异常 | Contracts |
| `LabelService.Application` | 应用层：应用服务（用例）、输入输出模型、内存缓存、对基础设施的抽象 | Contracts、Rendering、Domain |
| `LabelService.Infrastructure` | 基础设施层：EF Core、三种数据库、仓储实现、文件存储、密码哈希、令牌、后台任务 | 以上全部 |
| `LabelService.Api` | 接口层（原 `LabelService.Server`）：路由、鉴权、HTTP 细节、组装（Program） | 以上全部 |

- 领域层不引用 EF Core、ASP.NET Core、渲染引擎，只表达业务规则。
- 接口层只做“读请求 → 调应用服务 → 写响应”，不写业务规则、不直接访问数据库。
- 开放接口的热路径不变：模板、调用方、版式都从应用层的内存缓存取，请求路径上不访问数据库（R3）。

### 2. 命名（参照 ABP 的习惯，不引入 ABP 框架）

| 类别 | 命名 | 例 |
| --- | --- | --- |
| 实体（对应一张表） | `XxxEntity` | `TemplateEntity`、`ApiClientEntity` |
| 仓储接口 / 实现 | `IXxxRepository` / `XxxRepository` | `ITemplateRepository` |
| 应用服务接口 / 实现 | `IXxxAppService` / `XxxAppService` | `ITemplateAppService` |
| 输出模型 | `XxxDto` | `TemplateDto`、`ApiClientDto` |
| 输入模型 | `CreateXxxDto`、`UpdateXxxDto`、`GetXxxListInput` | `CreateApiClientDto` |
| 接口路由 | `XxxEndpoints` | `TemplateEndpoints` |
| 配置 | `XxxOptions` | `DataOptions` |
| 用户能修正的错误 | `UserFriendlyException` | |

### 3. 依赖注入

用 .NET 自带的容器，每层一个注册入口：`AddLabelApplication`、`AddLabelInfrastructure`、`AddLabelApi`，`Program` 只调用这三个。不引入 Autofac：自带容器所有 .NET 开发都熟悉，本项目用不到它的高级功能。

### 4. 多数据库

- 配置 `LabelService:Data:Provider` 选 `Sqlite`（默认）、`SqlServer`、`PostgreSql`（沿用原来的 `LabelService:Data` 配置节，已有部署不用改配置）。SQLite 用数据目录 `LabelService:Data:Directory` 里的文件；其他两种用连接字符串 `LabelService:Data:ConnectionString`，调用日志可以用单独的 `LogConnectionString`（不配时和主库同一个库，表名不冲突，迁移记录表分开）。数据目录仍然要有：素材文件、签名密钥放在那里。
- 每种数据库一套迁移（各自的派生 DbContext），模型只有一份。改了实体要给三种数据库各生成一次迁移（脚本见 backend/README.md），检查程序会核对三套迁移都和模型一致。
- SQLite 特有的功能只在 SQLite 下启用：连接 PRAGMA、`VACUUM INTO` 每日备份、升级前的快照和失败恢复、旧库（一期早期版本）的数据换算。SQL Server、PostgreSQL 的备份用数据库自己的工具（部署说明里写明），它们的迁移在事务里执行，失败自动回滚。
- 新增依赖：`Microsoft.EntityFrameworkCore.SqlServer`、`Npgsql.EntityFrameworkCore.PostgreSQL`（都是稳定版）。
- 换数据库不等于能多实例部署：模板、调用方、版式的内存缓存仍是单进程的，多实例要另做缓存同步（ADR 0003 的单实例约束不变）。

## 备选方案

- **直接用 ABP 框架**：约定最全，但要引入几十个依赖包，现有鉴权、权限、日志要按框架重写，启动和每次请求都有框架开销。只借用它的分层和命名习惯。
- **Autofac**：模块化注册和按约定批量注册方便一些，但多一个依赖，本项目的服务数量用自带容器足够。
- **不拆项目，只分文件夹**：改动小，但层之间没有编译期隔离，以后容易又写乱。
- **一套迁移兼容所有数据库**：EF Core 的迁移带数据库特有的类型和语法，一套迁移跑不了三种数据库。

## 影响

- `LabelService.Server` 改名为 `LabelService.Api`：verify.ps1、Dockerfile、部署脚本、检查程序、文档里的路径同步修改。
- AGENTS.md 的仓库地图和 R2（依赖方向）改成上面的分层；检查程序按新规则检查项目引用。
- 管理接口和开放接口的地址、请求和响应不变（重构前后用接口快照逐项比对）。
- 现有 SQLite 库照常升级：SQLite 迁移的编号不变，只是挪到 `Infrastructure` 里。
