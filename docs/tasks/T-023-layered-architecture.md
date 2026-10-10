# T-023 后端分层架构（DDD）与多数据库支持

状态：进行中
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

- [ ] 六个项目，依赖方向 Contracts ← Domain ← Application ← Infrastructure ← Api，Rendering 只被 Application 及以上引用；检查程序拦住反向引用
- [ ] 领域层不引用 EF Core、ASP.NET Core、渲染引擎；接口层不直接访问数据库
- [ ] 命名统一：实体 `XxxEntity`、仓储 `IXxxRepository`、应用服务 `IXxxAppService`、输出 `XxxDto`、输入 `CreateXxxDto` / `UpdateXxxDto`；接口不再返回匿名对象
- [ ] 依赖注入：每层一个注册入口（`AddLabelApplication`、`AddLabelInfrastructure`、`AddLabelApi`）
- [ ] 数据库可配置为 Sqlite（默认）、SqlServer、PostgreSql；三套迁移和模型一致；现有 SQLite 库照常升级
- [ ] 服务端检查在 SQLite 和 SQL Server 上都通过；PostgreSQL 视环境
- [ ] 接口快照（123 个调用的状态码、响应结构、错误码和消息）重构前后一致
- [ ] 基准门禁通过（基线不变）
- [ ] `verify.ps1` 通过（见 AGENTS.md）
- [ ] 相关文档已更新

## 不做

- 全部代码补齐注释、去掉代码里的任务号（下一个任务做）
- 多实例部署（缓存同步）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
