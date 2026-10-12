# T-038 字段的自动值（当前日期、有效期、批量序号、调用方等）

状态：进行中
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-034
设计书：§fields、§schema

## 目标

用户要求：按数据类型预设一些可用的默认值，比如当前时间，这样有些字段就不用接口传。做法见 ADR 0012：字段加可选的 `auto`，按类型给出常用预设（日期：今天、现在、今天加减 N 天/月/年；数字：批量序号、总条数；文本：调用方名称、请求号），调用方没传时服务端补上。

## 范围

可以新建、修改的文件：

- 后端：`Contracts/Templates/FieldDefinition.cs`、`Rendering/Validation/`（自动值规则、`FieldValidator`）、`Application/Rendering/`（时钟和时区配置）、`Application/OpenApi/`、`Application/ApiTest/`、`Application/Layouts/SheetLayoutAppService.cs`、`Application/Templates/`（发布前检查、样例数据）、检查程序
- 前端：`src/model/template.ts`、`src/model/autoValues.ts`（新建）、`src/model/request.ts`、`src/model/bindings.ts`（预览值）、`src/components/workspace/FieldsTab.vue`、`tests/checks.ts`
- 调用端：`src/LabelService.Client/TemplateSchema.cs`、README、CHANGELOG；演示程序把有自动值的字段当作不用填
- 文档：ADR 0012、`docs/contracts.md`、设计书 §fields、`backend/deploy/README.md`（时区）

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `backend/src/LabelService.Contracts/Templates/FieldDefinition.cs`：加 `Auto`
- `frontend/src/model/template.ts`：加 `auto`
- `backend/src/LabelService.Application/ApplicationServiceCollectionExtensions.cs`：登记自动值时钟
- `docs/contracts.md`、`docs/tasks/README.md`、`docs/adr/README.md`

## 验收标准

- [ ] 七种写法按类型生效，调用方传了值时用传的；同一请求的当前时间一致；批量序号从 1 开始、总数是条目数
- [ ] 时区可配置（`LabelService:Render:TimeZone`），不配用本机时区
- [ ] 发布前检查拦下不认识或类型不匹配的写法
- [ ] `/schema` 返回 `auto`，样例里不放有自动值的字段；SDK `TemplateField.Auto`
- [ ] 设计器“数据源”的默认值一栏可以选固定值或自动值，选了自动值取消必填；设计器预览显示自动值算出来的样子
- [ ] 后端、前端、调用端检查覆盖上面各项；改了渲染引擎，附基准对比
- [ ] `verify.ps1` 通过（见 AGENTS.md）
- [ ] 相关文档已更新

## 不做

- 跨请求递增的流水号（见 ADR 0012 备选方案）
- 任意表达式

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 认领。共享文件：FieldDefinition.cs、template.ts 各加一个可选属性（契约同一个提交改齐）；AddLabelApplication 登记一个单例；contracts.md、任务索引、ADR 索引各追加。
