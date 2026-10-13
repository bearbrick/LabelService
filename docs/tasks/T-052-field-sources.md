# T-052 字段数据来源及互斥的系统预设、手写值

状态：进行中
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
- 前端 model/template.ts、fieldSources.ts（新）、bindings.ts、request.ts、validate.ts；stores/designerFields.ts 及字段操作模块、workspace/FieldsTab.vue 与抽屉组件；检查、README。
- 客户端 TemplateSchema.cs、Internal 的 schema 解析、演示程序必填判断及检查；README、CHANGELOG、版本。
- 契约、架构、性能文档、设计书相关章节、ADR 0016、任务与 ADR 索引、本任务文件。

## 共享文件

- backend/src/LabelService.Contracts/Templates/FieldDefinition.cs、frontend/src/model/template.ts、docs/contracts.md、设计书契约章节：同一次契约提交同步三个部分。
- client/Directory.Build.props：新增可选 schema 属性递增预览版本。
- docs/tasks/README.md、docs/adr/README.md：追加索引，单独提交。

## 验收标准

- [ ] 接口传入禁用预设与手写值；模板提供的预设和手写值互斥，切换来源清理冲突配置，类型和必填联动。
- [ ] 新模板提供字段不接受同名接口覆盖；input 不使用回退值；旧字段 source 省略时保留原有优先级。
- [ ] 预览、发布校验、schema、调用样例、SDK/演示必填行为一致，手写 0 不视为空；旧预设不丢失。
- [ ] 字段表全宽，调用样例/字段定义接口按按钮打开可关闭抽屉；只读、撤销与自动保存保持正常。
- [ ] 新检查、Rendering 基准前后对比及全量 verify 通过，文档与任务记录同步。

## 不做

- 不改 Logo，不新增依赖，不升级或迁移数据库，不修改旧发布版本。
- 不做未测量的性能承诺或另建字段预编译缓存，不发布远程、不合并 main。

## 交接记录

- 2026-10-13 · Codex · 用户确认直接实现系统预设和手写值互斥，沿用此前确认的来源、全宽表格和抽屉方案。ADR 0016 记录新增可选来源，省略时保持旧回退语义，属于向后兼容变更。旧字段有回退值时展示“兼容：接口优先”，直到用户显式选择来源；不通过打开页面自动改变旧模板。共享文件如上登记。
