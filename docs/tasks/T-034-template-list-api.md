# T-034 开放接口列出可用模板，SDK 增加 ListTemplatesAsync

状态：进行中
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-009、T-011
设计书：§api、§sdk

## 目标

用户测试演示程序时问“模板编码怎么刷新”：开放接口只能按编码读字段定义，调用方不知道自己能用哪些模板，演示程序的模板编码下拉框只能写死两个示例。开放接口加 `GET /api/v1/templates`，返回这把 Key 有权使用的已发布模板（编码、名称、最新版本、尺寸）；SDK 加 `ListTemplatesAsync` / `ListTemplates`，演示程序（T-035）用它刷新下拉框，业务系统也能用它做模板选择。

## 范围

可以新建、修改的文件：

- 后端：`backend/src/LabelService.Contracts/Protocol/Messages.cs`（新增响应模型）、`backend/src/LabelService.Application/OpenApi/`、`backend/src/LabelService.Api/Endpoints/OpenApi/PublicApiEndpoints.cs`、检查程序
- 调用端：`client/src/LabelService.Client/`（`ILabelServiceClient`、`LabelServiceClient`、`TemplateSchema.cs`、`Internal/Payloads.cs`、README、CHANGELOG）、`client/tests/LabelService.Client.Checks/`
- 文档：`docs/contracts.md`、设计书 §api、§sdk 的接口列表（同步实现）

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `backend/src/LabelService.Contracts/Protocol/Messages.cs`：追加 `TemplateListResponse`、`TemplateSummary`
- `backend/src/LabelService.Api/Endpoints/OpenApi/PublicApiEndpoints.cs`：追加一条路由
- `client/Directory.Build.props`：`VersionSuffix` 改成 `preview.3`
- `docs/contracts.md`：开放接口表追加一行和响应示例
- `docs/tasks/README.md`：任务索引追加一行

## 验收标准

- [ ] `GET /api/v1/templates` 只列调用方有权使用的已发布模板，按编码排序；没有 Key 401；只读内存缓存，不访问数据库
- [ ] SDK `ListTemplatesAsync` / `ListTemplates`：触发 `CallCompleted`（操作名 ListTemplates）、出错抛 `LabelServiceException`，net462 能编译
- [ ] 后端检查：开发 Key 能列出示例模板；没授权任何分组的调用方列表为空
- [ ] 调用端检查：解析响应、空列表；端到端检查能列出 BOX_LABEL
- [ ] `verify.ps1` 通过（见 AGENTS.md）
- [ ] 相关文档已更新（契约、设计书接口列表、SDK README、CHANGELOG）

## 不做

- 分页和按名称搜索（模板上千时再加查询参数，响应外面包了一层对象，加字段不破坏兼容）
- 演示程序的改动（T-035）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 认领。共享文件：Contracts 的 Messages.cs 追加两个响应模型；PublicApiEndpoints 追加一条路由；client/Directory.Build.props 的预览号加 1；contracts.md、任务索引各追加一行。
