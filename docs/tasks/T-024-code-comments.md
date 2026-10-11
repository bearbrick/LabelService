# T-024 代码注释补齐，去掉代码里的任务号

状态：待验收
负责人：Claude 2026-10-11
里程碑：一期验收
依赖：T-023
设计书：—

## 目标

后来的开发人员读代码就能看懂：每个类型、函数、属性、参数都有完整的注释（包括私有的、检查程序里的），函数里关键的地方有说明；代码里不出现 T-016 这类任务号（任务号只出现在任务文件、提交信息和文档里）。规则由检查程序固定下来，以后漏写会被拦住。

## 范围

可以新建、修改的文件：

- `backend/src/**`、`backend/tests/**`（只改注释和检查；EF Core 生成的迁移不动）
- `client/src/**`、`client/samples/**`、`client/tests/**`（只改注释和检查）
- `frontend/src/**`、`frontend/tests/**`（只改注释和检查）
- `backend/tests/Directory.Build.props`、`client/tests`、`client/samples` 的项目文件：打开 XML 文档输出，供检查程序核对
- `backend/deploy/**`、`client/build/**` 里脚本的注释
- 补齐注释后超过 500 行（R11）的文件按职责拆分（只搬代码，不改行为），见交接记录

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `AGENTS.md`：R5 写明注释的要求和检查方式
- `verify.ps1`：只去掉注释里的任务号（如果有）
- `docs/tasks/README.md`：任务索引追加一行
- `backend/Directory.Build.props`、`client/Directory.Build.props`：如需统一打开 XML 文档输出

## 验收标准

- [x] C#：每个类型和成员（公开和不公开的，产品代码、演示程序、检查程序）都有三行写法的 `/// <summary>` 注释；方法、构造函数、记录的每个参数有 `<param>`，有返回值的有 `<returns>`，泛型参数有 `<typeparam>`；重写和接口实现可以用 `<inheritdoc />`
- [x] TypeScript / Vue：每个函数、类、接口、类型和接口的属性都有多行的 `/** … */` 注释，函数的参数和返回值写 `@param`、`@returns`
- [x] 函数里的关键步骤有注释（分支的理由、算法、容易误解的地方）
- [x] 产品代码、检查程序、项目文件和脚本里没有任务号
- [x] 后端、调用端、前端的检查程序各自核对上面的规则，漏写时失败并列出位置
- [ ] 接口快照不变，基准门禁通过（只改注释，行为不变）——快照一致；门禁在这台机器上没通过，T-023 的旧代码同样不通过，要在基线机器上重跑，见交接记录
- [x] `verify.ps1` 通过（见 AGENTS.md）
- [x] 相关文档已更新

## 不做

- 重写现有逻辑或改名（只加注释；发现的问题记到交接记录）
- EF Core 生成的迁移文件

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

### 2026-10-11 · Claude · 完成，待验收（基准门禁要在基线机器上重跑，见下）

**做了什么**

- 后端：六个产品项目、检查程序、基准程序的每个类型和成员（包括私有的）补齐 XML 注释；一行写法的 summary 全部改成三行。新增 `DocumentationChecks`：用反射列出程序集里的类型和成员，逐个到编译器输出的 XML 文档里核对 summary、param、returns、typeparam（`inheritdoc` 算有）；`[GeneratedRegex]` 的声明按文本核对；代码、项目文件、脚本（含 `client/`、`frontend/`、`verify.ps1`、`backend/deploy`）里不能有任务号。`tests/Directory.Build.props` 让检查程序和基准程序也输出 XML 文档。`dotnet run --project backend/tests/LabelService.Backend.Checks -- docs` 只查注释并列出全部缺漏。
- 调用端：SDK、WinForms 演示程序、检查程序补齐注释；`client/tests`、`client/samples` 打开 XML 文档输出；检查程序新增同样规则的 `DocumentationChecks`（演示程序按 net10.0-windows 的编译结果核对，从 Windows 桌面运行时目录解析 WinForms 程序集；没有桌面运行时的环境跳过并说明），也有 `-- docs` 模式。
- 前端：全部模块级的函数、常量、接口和类型（含属性）、类成员、store 的状态、组件的 props 和 emits 补齐多行 JSDoc，函数写 `@param`、`@returns`。新增 `tests/docs.ts`：用 TypeScript 编译器解析 .ts 和 .vue 的每个 `<script>`，用类型检查器判断有没有返回值；`npm run check` 还核对 `src/` 下的文件不超过 500 行。`tests/node.d.ts` 声明检查程序用到的几个 Node 函数（没有引入 @types/node）。`tests/checks.ts` 按主题改成函数（69 项检查的输出和改造前逐行一致）。
- 函数里的关键步骤：用脚本找出 25 行以上却没有一行行内注释的函数，在三个部分的 45 个左右的核心函数上补了说明（算法、分支理由、容易误解的地方）。
- 代码里的任务号全部去掉（`backend/deploy/monitoring`、`backend/tests/load`、`client` 的项目文件和打包脚本、前端源码等）。后端原来“代码里提到的任务号都有任务文件”那条检查随之去掉。
- 写注释时发现并改正的注释错误：`TemplateBenchmarks` 说“校验计入每次调用”，实际校验在准备阶段做一次，已改正说明（基准本身没动）。

**为什么拆了文件**（补齐注释后超过 R11 的 500 行，只搬代码、不改行为）

- 后端 Rendering：`RadicalCmap.cs` 里的 sfnt 表读写拆到 `Fonts/SfntTables.cs`、`Fonts/CmapSubtables.cs`；`TextLayout.cs` 的换行拆到 `Elements/LineBreaking.cs`（`MeasuredText`、`TextSpan`）。去掉注释后逐行比较，和 T-023 的代码只差类型的位置。
- 演示程序：`MainForm.cs` 的界面布局移到 `MainForm.Layout.cs`。
- 前端：`api/admin.ts` 按业务拆成 `accounts`、`businesses`、`templates`、`resources`、`clients`、`logs`，`admin.ts` 统一转导出（调用处不用改）；`model/layout.ts` 的排列操作移到 `layoutArrange.ts`；`editor/draw.ts` 拆出 `drawText.ts`、`drawCodes.ts`；`editor/stage.ts` 的视口部分成为基类 `StageViewport`（`stageViewport.ts`）；`stores/designer.ts` 变成统一入口，内容分到 `designerState`、`designerHistory`、`designerTemplate`、`designerElements`、`designerFields`（依赖方向 State ← History ← 其余，没有循环引用；`window.__labelDesigner` 不变）；`CanvasWorkspace.vue` 的直接编辑文本和拖放移到 `composables/useTextEdit.ts`、`useCanvasDrop.ts`。为此在 store 里加了 `findElement`、`viewCenter`、`requestFit`、`resetHistory` 四个小函数，`api/templates.ts` 加了 `PreviewInput` 类型。

**怎么验证的**

- 三个部分的注释检查全部通过：后端 8 个程序集、调用端 SDK、检查程序、演示程序、前端全部源码；任务号检查通过。
- 后端检查 449 项全过（注释检查合进全量运行）；调用端 38 项（不含端到端）；前端 71 项和打包。
- 接口快照：123 个调用和改造前逐项一致。
- 浏览器实测（前端拆分较多）：登录和首次改密码、13 个后台页面都能打开并加载数据；设计器里用真实鼠标拖动元素（吸附、撤销）、框选、改尺寸（二维码等比、节点缩放复位）、Ctrl+滚轮缩放和滚轮平移、双击直接编辑文本和 `{{` 字段补全、从元素库拖入、把字段拖到一维码上绑定，以及 store 的新建、复制、删除、微调、撤销重做、字段增删改、导入导出、自动保存到服务端；拼版设计器 8 个格子和预览图正常；控制台没有错误。
- WinForms 演示程序 `--smoke` 自检通过（内置模拟服务）。
- `verify.ps1` 全量通过：后端 449 项、调用端 47 项（含对真实服务的端到端）、前端 71 项和打包。

**基准门禁没通过，原因是机器状态，不是代码**

- 门禁结果：PDF、ZPL 共 10 项比基线慢 38%～77%，PNG 反而快 74%～82%（基线是 PNG 编码器换成 PngWriter 之前录的）。
- 在同一台机器上把 Rendering 换回 T-023 的版本对比（两种顺序各跑两轮，`ab-*.log`）：旧代码同样慢，PDF 在 2.8～5.4 ms、ZPL 在 0.9～3.4 ms 之间跳动；同一份代码几次运行能差一倍，说明机器在快慢状态之间切换（开着 Rider 等程序）。按中位数看两份代码差 7%～19%，在这种波动里分不出高低；而代码去掉注释后逐行比较，只差类型搬到了哪个文件。
- 按约定没有改基线。建议在录基线的那台机器（或同规格、空闲的机器）上重跑 `dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --gate`；PNG 已经比基线快很多，是否重录基线请用户决定（已列入待确认问题）。

**留给下一位的话**

- 改了代码要连注释一起写：三个部分的检查程序都会拦。只看注释时用 `-- docs` 模式（后端、调用端）或直接看 `npm run check` 的输出。
- 后端检查程序核对基准程序、调用端检查程序核对演示程序时用的是 Debug 编译结果，结果比源码旧时会提示先编译解决方案。
- 演示程序 `CodeGenerator` 的小出入（没改）：递增字段的值末尾没有数字时，生成的代码第 1 条是“值-0”，而演示程序自己批量生成时第 1 条是原值。
- 页面上看到的显示问题（字段表格“必填”“类型”列被截断等）留给 T-025。

**共享文件改动**：`AGENTS.md`（R5 写明注释要求和检查方式，前端约定补充 store 入口、视口基类、500 行和 composables，当前阶段一句）、`client/Directory.Build.props`（只去掉注释里的任务号）、`client/src/LabelService.Client/LabelErrorCodes.cs` 和 `Internal/Protocol.cs`（只加注释）、`backend/src/LabelService.Contracts/**`（只加注释）、`frontend/src/model/template.ts`（只加注释）。`docs/tasks/README.md` 在认领时追加了索引；`verify.ps1` 没有改（里面本来就没有任务号）。
