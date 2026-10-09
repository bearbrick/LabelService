# AGENTS.md：给 AI 编码助手的项目说明

本文件是所有 AI 助手（Claude Code、Codex、Cursor、GitHub Copilot、Gemini 等）的共同入口。`CLAUDE.md`、`GEMINI.md`、`.github/copilot-instructions.md` 都只是指向这里。给人看的介绍在 `README.md`，三个部分各有自己的 `README.md`。

本仓库可能有多个 AI 同时开发。下面的规则是为了让大家互不踩踏、改动可以自动验证。**规则和任务要求冲突时，以本文为准，并在任务文件里提出来。**

---

## 0. 开始前按顺序读

1. 本文件全文。
2. `docs/contracts.md`：模板 JSON 和开放接口的约定。
3. 你的任务文件 `docs/tasks/T-xxx-*.md`。**没有任务文件就不要改代码**：先问人，或者按 `docs/tasks/_template.md` 新建任务。
4. 任务涉及的那个部分的 `README.md`（`frontend/`、`backend/`、`client/`）。
5. 任务里写的设计书章节：`docs/design/label-service-design.html`。它是 HTML，按章节 id 查找：`#scope`、`#arch`、`#model`、`#designer`、`#schema`、`#fields`、`#render`、`#api`、`#nfr`（性能与监控）、`#sdk`、`#demo`、`#plan`。
6. `docs/architecture.md` 里和任务相关的模块。

## 1. 项目是什么

独立的**标签生成服务**：用户在网页设计器里画标签模板（文本、一维码、二维码、图片、图标、线、矩形），定义数据字段；业务系统传“模板编码 + JSON 数据”，服务返回 PNG、PDF 或 ZPL 文件。另有给第三方的 .NET SDK 和 WinForms 演示程序。

核心指标：**单张标签服务端处理 P95 ≤ 100 ms**（见 `docs/performance.md`）。

当前阶段：骨架已完成（T-000）。数据库用 SQLite、单实例部署（ADR 0003）。单张 PNG 链路可用，但只实现了线和矩形；文本、条码、图片、PDF、ZPL、批量、数据库、管理后台都在任务列表里（`docs/tasks/README.md`）。

## 2. 仓库地图

仓库分成三个**互不引用**的部分，只通过 HTTP 和模板 JSON 交互（ADR 0004）。一个任务通常只改一个部分。

| 路径 | 内容 | 备注 |
| --- | --- | --- |
| `frontend/` | 网页设计器、管理后台（Vue 3 + TypeScript + Vite + Konva） | npm 项目；`src/model/template.ts` 是契约镜像 |
| `backend/LabelService.Backend.slnx` | 后端解决方案 | |
| `backend/src/LabelService.Contracts/` | 模板 JSON 模型、错误码、HTTP 头、计时阶段 | **契约**，改动见 §6.4 |
| `backend/src/LabelService.Rendering/` | 绑定、校验、排版、绘制、输出编码 | 热路径，遵守 R3 |
| `backend/src/LabelService.Server/` | ASP.NET Core：开放接口、鉴权、模板存储、指标 | |
| `backend/tests/LabelService.Backend.Checks/` | 后端检查程序，**也负责跨三个部分的契约一致性检查** | 不是 xUnit，见 §5 |
| `backend/tests/LabelService.Benchmarks/` | BenchmarkDotNet | 必须 Release 运行 |
| `backend/samples/` | 示例模板、示例请求体 | 开发期服务启动时加载示例模板 |
| `client/LabelService.Client.slnx` | 调用端解决方案 | |
| `client/src/LabelService.Client/` | 给第三方的 SDK（net462、netstandard2.0、net8.0） | 遵守 R4 |
| `client/samples/LabelService.Demo.WinForms/` | SDK 演示程序（net48、net10.0-windows） | |
| `client/tests/LabelService.Client.Checks/` | 调用端检查程序 | 设置 `LABEL_SERVICE_URL` 时含端到端检查 |
| `docs/design/`、`docs/prototypes/` | 评审过的设计书和高保真原型（HTML） | 只做同步性修改，见 R10 |
| `docs/contracts.md`、`architecture.md`、`performance.md` | 规范文档 | |
| `docs/adr/` | 架构决策记录 | |
| `docs/tasks/` | 任务，每个任务一个文件 | 协作的中心，见 §6 |
| `verify.ps1` | 一次验证三个部分 | 交付前必跑 |

## 3. 常用命令

在仓库根目录执行。需要 .NET SDK 10.0.100 以上；前端需要 Node 20.19+ 或 22.12+。

**统一验证（交付前必须通过）**：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1                # 全部三个部分 + SDK 对真实服务的端到端检查
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1 -Part backend  # 只验证一个部分：backend / client / frontend
```

**后端**：

```powershell
dotnet build backend/LabelService.Backend.slnx
dotnet format backend/LabelService.Backend.slnx --verify-no-changes --no-restore
dotnet run --project backend/tests/LabelService.Backend.Checks
dotnet run --project backend/src/LabelService.Server          # http://localhost:5080，开发 Key：lbl_test_local_dev
curl.exe -o box.png -H "X-Api-Key: lbl_test_local_dev" -H "Content-Type: application/json" --data-binary "@backend/samples/requests/box-label.render.json" http://localhost:5080/api/v1/render
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --filter "*"   # 改了 Rendering 必跑
```

**调用端**：

```powershell
dotnet build client/LabelService.Client.slnx
dotnet format client/LabelService.Client.slnx --verify-no-changes --no-restore
dotnet run --project client/tests/LabelService.Client.Checks
```

**前端**：

```powershell
cd frontend; npm ci; npm run dev        # 开发，http://localhost:5173
cd frontend; npm run build              # 类型检查 + 打包
```

`dotnet format` 报格式问题时，对对应的解决方案运行一次不带 `--verify-no-changes` 的 `dotnet format` 修正，再检查改动是否只涉及你的文件。

## 4. 硬性规则

**R1 契约优先。** 模板 JSON、开放接口、错误码、HTTP 头、计时阶段名只在 `backend/src/LabelService.Contracts` 定义，后端其他地方引用常量，不写字面量。调用端和前端各有一份副本（见 §6.4）。改契约走 §6.4。

**R2 依赖方向。** 三个部分互不引用项目和文件：`backend/`、`client/` 的项目只引用本部分的项目；`frontend/` 不 import 本文件夹以外的文件。后端内部：Contracts ← Rendering ← Server，Contracts 不引用任何项目和包，Rendering 只引用 Contracts。检查程序会拦。

**R3 热路径。** 开放接口从收到请求到返回的代码（包括 Rendering 全部）：不访问数据库、磁盘、网络；不做一次性初始化；计时用 `Stopwatch.GetTimestamp()`。细则见 `docs/performance.md`。改 Rendering 必须附基准前后对比。

**R4 SDK 兼容。** `client/src/LabelService.Client` 必须能编译到 net462：不用 record、init、required、Index/Range、`ToHexStringLower` 之类新 API；不加 Newtonsoft.Json 或日志框架依赖。公开 API 变化要同步设计书第 10 节，并遵守语义化版本。

**R5 语言。** 代码注释、XML 注释、文档、给用户看的错误消息用**中文**；标识符、提交信息用**英文**。产品项目（各部分的 `src/`）公开成员必须写中文 XML 注释，缺了编译失败。

**R6 错误处理。** 调用方能修正的问题（数据、模板内容）返回 400 系列错误码：渲染里抛 `LabelRenderException(错误码)`，接口里用 `RenderCall.Fail`。程序缺陷不要吞掉、也不要包装成 400，让它以 `RENDER_FAILED` 暴露。

**R7 安全。** 不提交任何真实 Key、密码、证书、连接字符串；配置里的 API Key 只存 SHA-256。图片字段只收 base64，不支持 URL。不打包微软雅黑、宋体等系统字体（授权不允许），只用思源黑体、思源宋体（SIL OFL）。

**R8 依赖。** 新增 NuGet / npm 包要在任务文件里说明理由；NuGet 版本写在对应部分的 `Directory.Packages.props`，npm 版本写死不加 `^`；选发布满两周的稳定版。引入新的第三方库（不是升级）写 ADR。

**R9 范围。** 只改任务“范围”里的文件；“共享文件”先在交接记录登记再改。不顺手重构、不批量改格式、不改别人认领中的任务的文件。发现范围外的问题，记到交接记录或新建任务。

**R10 设计文档。** `docs/design/` 和 `docs/prototypes/` 是和老板评审过的设计，只做“让文档和实现一致”的同步修改，并在交接记录说明改了哪里；要改设计本身，先写 ADR 请人确认。

**R11 文件规模。** 产品源码单个文件少于 500 行（检查程序会拦）。快到上限就按职责拆分。

**R12 不提交生成物。** `bin/`、`obj/`、`node_modules/`、`dist/`、`BenchmarkDotNet.Artifacts/` 已在 .gitignore。`frontend/package-lock.json` 要提交。

**R13 不确定就停。** 遇到下面任何一种情况，不要猜：设计书没写清楚、和本文冲突、需要改契约中不兼容的部分、需要新增依赖、需要改别人认领的文件、验收标准做不到。在任务文件的交接记录里写清问题和你的建议，把状态改成 `阻塞`，结束本轮工作。

## 5. 编码约定

**C#（backend、client）**

- 遵循根目录 `.editorconfig`：4 空格、CRLF、文件范围命名空间、控制流一律带大括号、System 命名空间的 using 排最前。没有初始值的自动属性会被 `dotnet format` 展开成多行，这是公司约定，照做即可。
- `Nullable` 开启、`TreatWarningsAsErrors` 开启，0 警告。
- 内部类型用 `internal`；只有真正给别的程序集用的才 `public`。
- 新元素绘制器、新输出格式按 `docs/architecture.md` 的“扩展点”做：一个类型一个文件，在登记文件里加一行。
- 测试写进对应部分的检查程序（`backend/tests/LabelService.Backend.Checks` 或 `client/tests/LabelService.Client.Checks`）：按主题放在 `XxxChecks.cs`，用 `check("中文描述", 条件)`。需要像素检查的选“按设计必然空白”或“已实现元素”的位置。新行为必须有对应检查。
- 后端检查程序的 `Program` 在命名空间里，引用服务端入口要写 `global::Program`。

**TypeScript / Vue（frontend）**

- 严格模式，`npm run build`（含 vue-tsc）必须通过。
- 模板模型只从 `src/model/template.ts` 导入，长度一律 mm，换算只用 `src/model/units.ts`。
- 画布状态就是模板 JSON；不要另造一份元素状态再同步。

**提交信息**：英文 Conventional Commits，带任务号，scope 用部分或模块名。例：`feat(rendering): add text painter (T-001)`、`fix(server): return 400 for invalid utf-8 (T-000)`、`feat(frontend): snap to grid (T-013)`、`docs(tasks): claim T-002`。类型用 feat、fix、perf、refactor、test、docs、build、chore。

## 6. 多 AI 协作

### 6.1 任务的一生

1. **认领**：确认任务状态是 `待认领`，且“依赖”都已完成。把状态改成 `进行中`，负责人写“工具 + 日期”（如 `Codex 2026-10-10`），单独提交 `docs(tasks): claim T-xxx`。两个 AI 同时认领同一任务时，以先合并进主分支的为准，后到的换一个任务。
2. **实现**：小步提交，每个提交都能编译。共享文件的改动单独提交。
3. **自检**：逐项过 §7 完成定义，在任务文件里勾选验收标准。
4. **交接**：在“交接记录”最上面追加一条：做了什么、没做什么、遇到的坑、留给下一位的话、基准结果。状态改成 `待验收`。由人或另一个 AI 验收后改成 `已完成`。
5. **中途停下**（额度用完、上下文太长、被打断）：也要写交接记录，状态保持 `进行中`，写清下一步从哪里接着做。

三个部分互相独立，前端、后端、调用端的任务可以同时由不同的 AI 做。

### 6.2 分支与合并

- 使用 git 时，每个任务一个分支：`ai/<工具>/<任务号>-<短名>`，如 `ai/codex/T-001-text`。从主分支拉，做完提合并请求。
- 只在自己的任务分支上提交。合并到主分支、推送远程、改写历史，需要人确认。
- 合并冲突时：登记类文件（`ElementPainters.cs`、`OutputDocuments.cs`、`Directory.Packages.props`、任务索引）保留双方的行；其他文件拿不准就停下（R13）。

### 6.3 共享文件

这些文件多个任务都可能改，是冲突的高发区。改之前在任务文件“共享文件”里列出，改动尽量只**追加**行，单独提交：

- 全仓库：`.editorconfig`、`global.json`、`verify.ps1`、`AGENTS.md`、`docs/contracts.md`、`docs/tasks/README.md`
- 后端：`backend/LabelService.Backend.slnx`、`backend/Directory.Build.props`、`backend/src/Directory.Build.props`、`backend/Directory.Packages.props`、`backend/src/LabelService.Contracts/**`、`backend/src/LabelService.Rendering/Elements/ElementPainters.cs`、`backend/src/LabelService.Rendering/Output/OutputDocuments.cs`、`backend/src/LabelService.Server/Program.cs`、`backend/src/LabelService.Server/Endpoints/PublicApiEndpoints.cs`
- 调用端：`client/LabelService.Client.slnx`、`client/Directory.Build.props`、`client/Directory.Packages.props`、`client/src/LabelService.Client/LabelErrorCodes.cs`、`client/src/LabelService.Client/Internal/Protocol.cs`
- 前端：`frontend/package.json`、`frontend/src/model/template.ts`、`frontend/src/samples/box-label.json`

### 6.4 契约变更

契约的权威定义在后端 Contracts，另外两个部分有副本：

| 契约内容 | 后端（权威） | 调用端副本 | 前端副本 |
| --- | --- | --- | --- |
| 错误码 | `Protocol/ErrorCodes.cs` | `LabelErrorCodes.cs` | — |
| HTTP 头 | `Protocol/HeaderNames.cs` | `Internal/Protocol.cs` | — |
| 模板 JSON 模型 | `Templates/*.cs` | — | `src/model/template.ts` |
| 示例模板 | `backend/samples/templates/box-label.json` | — | `src/samples/box-label.json` |

- **向后兼容**（新增可选属性、新增错误码、新增元素类型、新增响应头）：在同一个提交里同时改后端 Contracts、调用端和前端的副本、`docs/contracts.md`、设计书对应章节，然后运行完整的 `verify.ps1`。后端检查程序会按文本读取副本，核对错误码、HTTP 头、元素类型、示例模板是否同步。
- **不兼容**（改名、删除、改语义或默认值）：先写 ADR（状态“提议”），在任务交接记录里请人确认，确认后才改，并升级 `schemaVersion` 或接口版本。

### 6.5 架构决策（ADR）

跨部分、难回退、以后有人会问“为什么”的决定写 ADR，格式和时机见 `docs/adr/README.md`。

## 7. 完成定义

交付前逐项确认：

- [ ] `verify.ps1` 通过：只改了一个部分可以用 `-Part`，改了契约、文档或多个部分必须跑全量
- [ ] 新行为有对应的新检查
- [ ] 改了 Rendering：基准前后对比写进交接记录，必要时更新 `docs/performance.md` 的基线
- [ ] 改了契约：§6.4 表里的文件都已同步
- [ ] 文档同步：对应部分的 README、架构、性能、契约、设计书中受影响的部分
- [ ] 任务文件：验收标准已勾选、交接记录已追加、状态已更新
- [ ] 没有提交密钥、生成物、和任务无关的改动

## 8. 术语

| 中文 | 代码里 | 说明 |
| --- | --- | --- |
| 前端 / 后端 / 调用端 | `frontend/` / `backend/` / `client/` | 仓库的三个部分 |
| 模板 | `LabelTemplate` | 一种标签的设计，如“成品箱标” |
| 模板编码 | `templateCode` / `Code` | 全局唯一，开放接口用它找模板 |
| 模板版本 | `version` | 发布产生新版本，发布后不可修改 |
| 字段 | `FieldDefinition` | 调用方要传的一项数据（key + 类型） |
| 绑定 | `{{key:format}}` | 元素里引用字段的写法 |
| 元素 | `LabelElement` | 画布上的对象，7 种 |
| 绘制器 | `IElementPainter` | 一种元素的服务端渲染实现 |
| 输出文档 | `OutputDocument` | 一种输出格式的实现 |
| 打印点 | dot | 打印机的最小单位；点数 = mm ÷ 25.4 × DPI |
| 箱标 / 唛头 | — | 箱标是贴在箱子上的小标签（如 100×60mm）；唛头是外箱的大标识，常用 A4 / A5 |
| 调用方 | `ApiClient` | 一个接入的业务系统，一把 API Key |
| 慢调用 | `slow=True` | 单张服务端处理超过 100 ms |
| 阶段 | `TimingStages` | auth、tpl、validate、layout、draw、encode、total |

## 9. 已知的坑

- **Windows 命令行发中文**：bash 或 cmd 直接 `--data '{"partName":"中文"}'` 可能按 GBK 发送。用 UTF-8 文件加 `--data-binary @文件`。服务端对非法 UTF-8 返回 `FIELD_TYPE_INVALID`。
- **PowerShell 脚本要带 BOM**：Windows PowerShell 5.1 只有带 BOM 才按 UTF-8 读取中文，`.editorconfig` 已对 `*.ps1` 设置 `utf-8-bom`。运行脚本用 `-ExecutionPolicy Bypass`。
- **csproj 里的 XML 注释不能出现 `--`**（比如写命令行参数），否则项目加载失败。
- **System.Text.Json 多态**：元素基类的 `Type` 属性由构造函数传入并标 `[JsonIgnore]`，派生类不要 override 一个同名属性，否则和 `type` 鉴别器冲突。
- **SDK 在 netstandard2.0 下没有可空性特性**：`string.IsNullOrEmpty` 不会帮编译器推断非空，写显式判断。
- **行尾**：`.editorconfig` 要求 CRLF。用脚本生成的文件如果是 LF，`dotnet format --verify-no-changes` 会报错，跑一次 `dotnet format` 即可。
- **WebApplicationFactory 不认识 .slnx**：后端检查程序里已经显式指定内容根目录，新增服务端检查时沿用 `ServerChecks` 里的 `ServerFactory`。
- **SkiaSharp 在 Linux** 需要 `SkiaSharp.NativeAssets.Linux.NoDependencies`（Server 已引用）。
- **条码模块宽度必须是整数个打印点**，否则看着正常、扫不出来。
- **SQLite**：数据目录不能放网络共享盘，同一数据目录只能有一个服务进程；时间列存 UTC `DateTime`，不用 `DateTimeOffset`；检查程序用临时数据目录。
- **开发 Key** `lbl_test_local_dev` 只在 Development 环境有效；后端检查程序用 `WebApplicationFactory` 启动服务，默认就是 Development；`verify.ps1` 启动服务时也会设置 Development。
