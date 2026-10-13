# T-052 字段数据来源及互斥的系统预设、手写值

状态：待验收
负责人：Codex 2026-10-13
里程碑：一期验收
依赖：T-038 已实现的自动值能力
设计书：§fields、§schema、§sdk

## 目标

按用户确认方案，将字段的接口输入、模板提供分清，系统预设与手写值互斥；字段表占满工作区，调用样例与字段定义接口使用按钮抽屉。旧模板未声明来源时保持原接口优先的回退规则，不修改已发布版本。

## 范围

- 后端 Contracts/Templates/FieldDefinition.cs：可选 source（input/template）。
- Rendering/Validation/FieldValidator.cs：按显式来源取值，保留旧行为；相关检查和基准前后对比。
- Application/Templates/TemplateChecker.cs、TemplateSampleData.cs、OpenApi/LabelRenderAppService.cs：配置约束、预览与 schema。
- 前端 model/template.ts、fieldSources.ts（新）、bindings.ts、request.ts、validate.ts；stores/designerFields.ts 及字段操作模块、workspace/FieldsTab.vue 与抽屉组件、components/FieldList.vue 的来源提示；检查、README。
- frontend/src/editor/draw.ts：图片字段预览按来源取模板手写值，不被旧样例覆盖。
- 客户端 TemplateSchema.cs、Internal 的 schema 解析、演示程序必填判断及检查；README、CHANGELOG、版本。
- 契约、架构、性能文档、设计书相关章节、ADR 0016、任务与 ADR 索引、本任务文件。

## 共享文件

- backend/src/LabelService.Contracts/Templates/FieldDefinition.cs、frontend/src/model/template.ts、docs/contracts.md、设计书契约章节：同一次契约提交同步三个部分。
- client/Directory.Build.props：新增可选 schema 属性递增预览版本。
- docs/tasks/README.md、docs/adr/README.md：追加索引，单独提交。

## 验收标准

- [x] 接口传入禁用预设与手写值；模板提供的预设和手写值互斥，切换来源清理冲突配置，类型和必填联动。
- [x] 新模板提供字段不接受同名接口覆盖；input 不使用回退值；旧字段 source 省略时保留原有优先级。
- [x] 预览、发布校验、schema、调用样例、SDK/演示必填行为一致，手写 0 不视为空；旧预设不丢失。
- [x] 字段表全宽，调用样例/字段定义接口按按钮打开可关闭抽屉；只读、撤销与自动保存保持正常。
- [x] 新检查、Rendering 基准前后对比及全量 verify 通过，文档与任务记录同步。

## 不做

- 不改 Logo，不新增依赖，不升级或迁移数据库，不修改旧发布版本。
- 不做未测量的性能承诺或另建字段预编译缓存，不发布远程、不合并 main。

## 交接记录

- 2026-10-13 · Codex · 实现可选 source、发布冲突校验、按来源取值与 schema 输出；前端系统预设/手写值分列互斥，常用日期预设自动采用 date 类型，0 保留，手写 now 不当表达式。接口字段禁用两项；模板字段无需接口样例，文本及图片预览直接取模板内容。旧回退字段保持兼容状态，只在显式操作后设置新来源。字段修改复用 store 的 commit，沿用撤销、只读恢复及自动保存，未改历史发布版本。字段表全宽，接口资料移入抽屉，支持 Esc、Tab 焦点循环和返回入口。SDK 新增 Source/RequiresInput，演示单张/批量均不提交模板字段；版本 preview.5。
  - 验证：全量 verify.ps1 通过，后端 529、调用端 137（含独立真实服务端到端）、前端 117，格式、类型及打包均通过；日志 artifacts/field-sources/verify-final.log。新增检查覆盖同名覆盖禁止、空值、0、旧回退、预设互斥、类型、schema、实际 PNG 内容一致。两种演示运行时 smoke 各 31 项通过。
  - 浏览器：用户登录后核对实际 BOX_LABEL 字段页、禁用状态及两个抽屉关闭/焦点返回；未编辑或发布用户模板。1440×900 下全部列可见，证据 artifacts/field-sources/04-fields-desktop.png；临时视口已恢复。前端、后端、net48 演示程序均恢复运行，运行进程登记 artifacts/local-run/processes.json。
  - 性能：改动前对历史基线的完整门禁已有 10 项失败；保留仓库基线。会话改动前后 18 项完整比较，17 项在 20% 内；仅渲染箱标 PDF 4.767→5.957 ms（+25%），该项校验在准备阶段、不计入计时，单项复跑中位数 5.115 ms（+7%）。全量门禁不能记为通过；单项命令因基线覆盖检查返回 3，实际测量值保留。详见 docs/performance.md；没有承诺来源判断能提速，也没有预编译缓存。
  - 文档：同步契约、架构、三个 README、SDK CHANGELOG；设计书仅同步 §fields、§sdk 的已授权来源/页面/SDK 实现。SDK 与 Windows 扩展的 nupkg、snupkg、完整 zip 已打包到 artifacts/field-sources/package（内部试用、未签名）；不提交生成物，不推送或合并 main。待用户实际使用验收，历史基线门禁异常留待独立性能任务处理。

- 2026-10-13 · Codex · 用户确认直接实现系统预设和手写值互斥，沿用此前确认的来源、全宽表格和抽屉方案。ADR 0016 记录新增可选来源，省略时保持旧回退语义，属于向后兼容变更。旧字段有回退值时展示“兼容：接口优先”，直到用户显式选择来源；不通过打开页面自动改变旧模板。共享文件如上登记。
