# T-009 调用方管理与调用日志

状态：待认领
负责人：—
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

## 验收标准

- [ ] 日志字段按设计书 render_log：request_id、client_id、template_code、version_no、format、item_count、duration_ms、stage_ms_json、is_slow、status、error_code；不存业务数据
- [ ] 写日志失败不影响生成结果，只记警告
- [ ] 日志队列满时丢弃并计数（指标），不能拖慢请求
- [ ] Key 只存 SHA-256；重置后旧 Key 立即失效
- [ ] 后台按时间、模板、调用方、结果筛选日志有索引，100 万行时查询 < 200 ms
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 后台页面（见 T-014）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
