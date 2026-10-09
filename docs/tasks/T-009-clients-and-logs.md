# T-009 调用方管理与调用日志

状态：待验收
负责人：Claude 2026-10-09
里程碑：M3
依赖：T-008（数据库）
设计书：§3 api_client、render_log；§8 鉴权；§9 日志与审计；ADR 0003

## 目标

调用方和 API Key 存数据库，可以限制模板、停用、重置；每次生成写一条调用日志，供后台查询和对账。

## 范围

可以新建、修改的文件：

- `backend/src/LabelService.Server/Auth/`：从数据库加载调用方到内存，变更时刷新；Key 生成规则（测试环境 `lbl_test_` 前缀）
- 新建 `backend/src/LabelService.Server/Logging/`：render_log 写入独立的库文件 `label-log.db`，**异步批量写**（Channel + 后台服务，每批一个事务），不阻塞请求
- 日志清理：保留期可配置（默认 180 天），每天清理一次
- `backend/src/LabelService.Server/Endpoints/RenderCall.cs`：在 `Finish` 里投递日志

## 共享文件

- `backend/src/LabelService.Server/Endpoints/RenderCall.cs`
- `backend/src/LabelService.Server/Program.cs`
- `backend/src/LabelService.Server/Data/LabelDbContext.cs`：加 api_client 表；UTC 和下划线列名两个约定抽成静态方法给日志库共用
- `backend/src/LabelService.Server/Endpoints/PublicApiEndpoints.cs`、`BatchRenderEndpoint.cs`：传入日志队列、调用方 ID、模板版本
- `TemplateStoreException` 改名为 `ManagementException`（挪到 `Server/ManagementException.cs`），模板和调用方管理共用
- `AGENTS.md`：第 1 节当前阶段

## 验收标准

- [x] 日志字段按设计书 render_log：request_id、client_id、template_code、version_no、format、item_count、duration_ms、stage_ms_json、is_slow、status、error_code；不存业务数据
- [x] 写日志失败不影响生成结果，只记警告
- [x] 日志队列满时丢弃并计数（指标），不能拖慢请求
- [x] Key 只存 SHA-256；重置后旧 Key 立即失效
- [x] 后台按时间、模板、调用方、结果筛选日志有索引，100 万行时查询 < 200 ms
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 后台页面（见 T-014）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收。
  - 做了什么：调用方：`Data/ApiClientRecord.cs`（api_client：名称唯一、Key 哈希唯一、Key 前缀只用来辨认、允许的模板逗号分隔、启停）、迁移 `AddApiClients`；`Auth/ApiKeyAuthenticator.cs` 改成配置 + 数据库合并的内存快照（FrozenDictionary，读不加锁，变更后整体替换）；`Auth/ApiClientRepository.cs`（新建返回一次 Key 原文、改权限、启停、重置 Key，每次变更后重新加载）。调用日志：`Data/LogDbContext.cs`（label-log.db，render_log 按设计书字段，另加 endpoint、client_name，复合索引：时间、模板+时间、调用方+时间、结果+时间、request_id）、迁移在 `Data/LogMigrations`；`Logging/RenderLogWriter.cs`（有界 Channel，满了丢弃并计数）、`RenderLogService.cs`（后台按批写入、每批一个事务；启动时和之后每天清理过期日志）、`RenderLogQuery.cs`（筛选 + keyset 翻页）。`RenderCall.Finish` 投递日志；指标加 `label_log_dropped_total`。
  - 检查：新增 `ClientLogChecks.cs`（15 项）：Key 前缀、只存哈希、模板限制、名称重复、停用和重置立即生效、配置 Key 仍可用；成功和失败的日志字段、不存业务数据、筛选和翻页、清理；20 万行查询性能；队列满丢弃；日志库写不进去时后台服务不停。`StorageChecks` 的“开放接口不访问数据库”改为只数主库的命令（日志在后台写）。
  - 性能：100 万行时各种筛选取一页最慢 1.1 ms（要求 < 200 ms）。已写进 docs/performance.md。
  - 说明：配置文件里的调用方（开发 Key）没有数据库 ID，日志里 client_id 为空、client_name 有值。模板来源是 Files 时不加载数据库调用方、不写调用日志。
  - 留给下一位：T-014 的管理接口直接用 `ApiClientRepository`、`RenderLogQuery`，异常 `ManagementException` 转 400/409；新建和重置 Key 时只在响应里返回一次原文。
- 2026-10-09 · Claude · 认领。计划：api_client 进 label.db，鉴权用内存快照，增改、重置 Key 后整体替换快照；render_log 进独立的 label-log.db，请求结束时投递到有界 Channel，后台服务按批写入、每天清理过期日志；队列满丢弃并计数。
