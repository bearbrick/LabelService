# 架构说明

标签生成服务把“模板 + 数据”变成可直接打印的文件（PNG、PDF、ZPL）。业务系统通过开放接口或客户端 SDK 调用；网页设计器负责画模板。完整背景见[设计书](design/label-service-design.html)。

## 三个部分

仓库分成三个互不引用的部分，只通过 HTTP 和模板 JSON 交互（[ADR 0004](adr/0004-three-parts.md)）：

```
  frontend/（Vue 设计器、管理后台）           client/（SDK、WinForms 演示程序）
        │ HTTP + 模板 JSON                          │ HTTP（不引用后端程序集）
        └──────────────────┬────────────────────────┘
                           ▼
  backend/ ┌──────────────────────────────────────────────────────────┐
           │ LabelService.Server     开放 API、管理 API、鉴权、模板存储、指标 │
           ├──────────────────────────────────────────────────────────┤
           │ LabelService.Rendering  排版、绘制、输出编码（SkiaSharp、ZXing.Net） │
           ├──────────────────────────────────────────────────────────┤
           │ LabelService.Contracts  模板 JSON 模型、协议常量（契约，零依赖）    │
           └──────────────────────────────────────────────────────────┘
```

| 部分 | 解决方案 / 入口 | 编译配置 | 检查 |
| --- | --- | --- | --- |
| `frontend/` | `package.json` | `tsconfig.json`、`vite.config.ts` | `npm run build`（vue-tsc 类型检查） |
| `backend/` | `LabelService.Backend.slnx` | `backend/Directory.Build.props`、`Directory.Packages.props` | `backend/tests/LabelService.Backend.Checks` |
| `client/` | `LabelService.Client.slnx` | `client/Directory.Build.props`、`Directory.Packages.props` | `client/tests/LabelService.Client.Checks` |

- 后端内部依赖只能向下：Contracts 不引用任何项目和包；Rendering 只引用 Contracts；Server 引用两者。
- 契约的权威定义在后端 Contracts。调用端有错误码和 HTTP 头的副本（[ADR 0002](adr/0002-client-sdk-standalone.md)），前端有模板模型和示例模板的副本。
- **跨部分的一致性由后端检查程序负责**：它按文本读取 `client/` 和 `frontend/` 的源码，核对错误码、HTTP 头、元素类型、示例模板；同时检查三个部分之间没有项目引用或跨文件夹 import。
- SDK 和服务端是否真正对得上，由调用端检查程序的端到端用例验证；根目录 `verify.ps1` 全量运行时会临时启动后端服务执行它们。

## 单张生成的处理流程

`POST /api/v1/render` 的每一步都单独计时，阶段名就是 Server-Timing 里的名字。下表路径相对于 `backend/src/`。

| 阶段 | 做什么 | 代码位置 |
| --- | --- | --- |
| `auth` | 按 X-Api-Key 的 SHA-256 在内存里查调用方 | `LabelService.Server/Auth/ApiKeyAuthenticator.cs` |
| `validate` | 读请求体，检查 format、dpi、copies | `LabelService.Server/Endpoints/PublicApiEndpoints.cs` |
| `tpl` | 从内存取已发布模板，检查调用方能否使用 | `LabelService.Server/Templates/ITemplateStore.cs` |
| `validate` | 按字段定义校验数据、转换类型 | `LabelService.Rendering/Validation/FieldValidator.cs` |
| `layout` | 替换绑定、文本测量、条码编码 | `LabelService.Rendering/LabelRenderer.cs` → 各 `IElementPainter.Layout` |
| `draw` | 按元素顺序画到画布，统一处理旋转 | 各 `IElementPainter.Draw` |
| `encode` | 编码成 PNG / PDF / ZPL | `LabelService.Rendering/Output/*OutputDocument.cs` |
| `total` | 写响应头、记指标和日志 | `LabelService.Server/Endpoints/RenderCall.cs` |

错误统一用 `ErrorResponse` 返回（`LabelService.Server/Endpoints/ApiErrors.cs`），HTTP 状态码由 `ErrorCodes.StatusCodes` 决定。

## 目录职责

**后端（`backend/`）**

| 路径 | 职责 |
| --- | --- |
| `src/LabelService.Contracts/Templates/` | 模板 JSON 模型：模板、纸张、字段、7 种元素、样式和枚举 |
| `src/LabelService.Contracts/Protocol/` | 错误码、HTTP 头、计时阶段、取值上限、请求和响应体 |
| `src/LabelService.Contracts/LabelJson.cs`、`LabelUnits.cs` | 统一的 JSON 设置；mm、pt、点的换算 |
| `src/LabelService.Rendering/Binding/` | `{{key:format}}` 绑定替换 |
| `src/LabelService.Rendering/Validation/` | 字段校验和类型转换 |
| `src/LabelService.Rendering/Elements/` | 每种元素一个绘制器，`ElementPainters.cs` 登记 |
| `src/LabelService.Rendering/Output/` | 每种格式一个输出文档，`OutputDocuments.cs` 登记 |
| `src/LabelService.Rendering/Diagnostics/` | 分阶段计时、Server-Timing 生成 |
| `src/LabelService.Server/Endpoints/` | 开放接口路由、请求收尾（计时、指标、日志）、错误响应 |
| `src/LabelService.Server/Auth/` | API Key 识别 |
| `src/LabelService.Server/Templates/` | 已发布模板的读取；目前从 `samples/templates` 加载 |
| `src/LabelService.Server/Diagnostics/` | 指标（System.Diagnostics.Metrics） |
| `samples/` | 示例模板、示例请求体 |
| `tests/LabelService.Backend.Checks/` | 后端检查程序，含跨部分的契约一致性和仓库规范检查 |
| `tests/LabelService.Benchmarks/` | BenchmarkDotNet 渲染基准 |

**调用端（`client/`）**

| 路径 | 职责 |
| --- | --- |
| `src/LabelService.Client/` | SDK 公开 API；`Internal/` 放协议副本和请求体生成；`Printing/` 放打印辅助类 |
| `samples/LabelService.Demo.WinForms/` | SDK 演示程序 |
| `tests/LabelService.Client.Checks/` | 调用端检查程序：假服务端用例、协议解析、架构规则、端到端用例 |

**前端（`frontend/`）**

| 路径 | 职责 |
| --- | --- |
| `src/model/` | 模板 JSON 的 TypeScript 镜像、单位换算 |
| `src/components/` | 画布等组件 |
| `src/samples/` | 示例模板副本（管理后台完成前演示用） |

## 扩展点

**新增一种元素的渲染**：在 `backend/src/LabelService.Rendering/Elements/` 新建 `XxxPainter.cs` 实现 `IElementPainter`（`Layout` 做计算，`Draw` 只画），在 `ElementPainters.cs` 登记一行，在后端检查程序的 `RenderChecks.cs` 补像素检查。绘制器必须无状态、线程安全，坐标一律是打印点，旋转已经由 `LabelRenderer` 处理。

**新增一种输出格式**：在 `backend/src/LabelService.Rendering/Output/` 新建 `XxxOutputDocument.cs` 继承 `OutputDocument`，在 `OutputDocuments.cs` 登记。画布坐标必须保持“1 单位 = 1 打印点”，矢量格式自己做缩放。

**替换模板存储**：实现 `ITemplateStore`，在 `Program.cs` 换掉 `FileTemplateStore`。请求路径上只能查内存，数据库读写放在启动预热和发布时。

## 状态与部署

- 初版用 SQLite、单实例部署，前期不考虑高可用（[ADR 0003](adr/0003-sqlite-single-instance.md)）。数据目录里有 `label.db`（模板等）、`label-log.db`（调用日志）和素材文件，不能放网络共享盘。
- 服务进程本身不保存请求状态，以后换 SQL Server 后可以多实例。
- 开发环境用 `appsettings.Development.json` 里的测试调用方，Key 是 `lbl_test_local_dev`，配置里只存它的 SHA-256。
- 部署（Docker 或 Windows 服务）、预热、并发控制见 T-017；监控接入见 T-010。
