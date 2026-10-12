# T-038 字段的自动值（当前日期、有效期、批量序号、调用方等）

状态：待验收
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

- [x] 七种写法按类型生效，调用方传了值时用传的；同一请求的当前时间一致；批量序号从 1 开始、总数是条目数
- [x] 时区可配置（`LabelService:Render:TimeZone`），不配用本机时区
- [x] 发布前检查拦下不认识或类型不匹配的写法
- [x] `/schema` 返回 `auto`，样例里不放有自动值的字段；SDK `TemplateField.Auto`
- [x] 设计器“数据源”的默认值一栏可以选固定值或自动值，选了自动值取消必填；设计器预览显示自动值算出来的样子
- [x] 后端、前端、调用端检查覆盖上面各项；改了渲染引擎，附基准对比
- [x] `verify.ps1` 通过（见 AGENTS.md）
- [x] 相关文档已更新

## 不做

- 跨请求递增的流水号（见 ADR 0012 备选方案）
- 任意表达式

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 完成，待验收。
  - 契约：`FieldDefinition.Auto`（后端、前端 `template.ts`、SDK `TemplateField.Auto` 同一个提交），`docs/contracts.md` 加了写法表，设计书 §fields 同步。向后兼容，不升 schemaVersion。
  - 渲染引擎：`Validation/AutoValues.cs`（写法校验和取值，手写解析不用正则）；`FieldValidator.Validate` 多一个可选参数 `AutoValueContext`，取值顺序 条目 → 公共字段 → 自动值 → default。不传上下文时（预览、试渲染、预热、检查程序）按本机当前时间，调用方写“示例调用方”、请求号写 PREVIEW。
  - 应用层：`AutoValueClock`（单例，`TimeProvider` + `LabelService:Render:TimeZone`，时区不存在时警告并用本机时区）；模板没有自动值字段时不建上下文。开放接口单张、批量（整批共用一个当前时间，total 是条目数）、接口调试（调用方写“接口调试”）、服务端预览都传上下文。请求号：接口层把 X-Request-Id（没带时用跟踪号）写进 `RenderTrace.RequestId`。`/schema` 返回 auto，sample 里去掉有自动值的字段；试渲染的样例数据也不给这些字段（显示自动值算出来的样子）。发布前检查拦下不认识或类型不匹配的写法。
  - 前端：`model/autoValues.ts`（预设、解析、和后端一致的校验、预览值；加月按 .NET 的月底规则）；数据源页默认值一栏改成“固定值 / 自动值”下拉，带 N 的预设旁边填 N，选了自动值取消必填并禁用必填框；设计器预览、字段面板（“自动”标签）、调用样例、模板检查都跟着改。
  - 调用端：SDK 的 `TemplateField.Auto`（CHANGELOG 记在还没发布的 preview.3 里）；演示程序把有自动值的字段当作不用填（不标红、批量表头不加 *）。
  - 部署说明：Docker 要配 `LabelService__Render__TimeZone=Asia/Shanghai`（容器默认 UTC）。
  - 检查：后端 16 项（七种写法、月底、批量序号、传值优先、类型不匹配、时区换算、无效时区、发布检查、/schema、真实调用）；前端 9 项；`verify.ps1` 全量通过：后端 517、调用端 54、前端 104。浏览器（无头 Edge，保存草稿的请求在浏览器里拦下，没改测试环境的数据）：选“N 天后”出现 N=30、必填框禁用、调用样例去掉了这两个字段，设计器画布显示“日期：2026-11-11”（今天加 30 天）、“批次：PREVIEW”。
  - 基准（同一次会话 新/旧/新，单位 µs）：BoxLabelPng 203dpi 944 / 984 / 910，300dpi 1731 / 1786 / 1706；UniqueBox 300dpi 2116 / 1981 / 2008。差别在两次“新”之间的波动以内（约 5%），分配不变。
  - 没做：流水号、任意表达式（ADR 0012 备选方案）。数据源页里有自动值的字段仍然显示样例值输入框（样例值在切回固定值时还有用），预览不用它。

- 2026-10-12 · Claude · 认领。共享文件：FieldDefinition.cs、template.ts 各加一个可选属性（契约同一个提交改齐）；AddLabelApplication 登记一个单例；contracts.md、任务索引、ADR 索引各追加。
