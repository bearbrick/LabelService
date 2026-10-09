# 后端（backend）

标签生成服务的服务端：模板 JSON 契约、渲染引擎、ASP.NET Core 服务。解决方案 `LabelService.Backend.slnx`，.NET 10。

```
backend/
├─ LabelService.Backend.slnx
├─ Directory.Build.props          后端全部项目的编译设置
├─ Directory.Packages.props       后端的 NuGet 版本
├─ src/
│  ├─ LabelService.Contracts/     模板 JSON 模型、错误码、HTTP 头（契约，零依赖）
│  ├─ LabelService.Rendering/     渲染引擎：绑定、校验、排版、绘制、输出
│  └─ LabelService.Server/        开放接口、鉴权、模板存储、指标
├─ tests/
│  ├─ LabelService.Backend.Checks/  检查程序（含跨前端、调用端的契约一致性检查）
│  └─ LabelService.Benchmarks/      渲染基准
├─ assets/fonts/                 思源黑体、思源宋体（文件不提交，准备方法见那里的 README）
├─ assets/icons/                 GB/T 191 图标（SVG，和设计器同一套）
├─ assets/images/                内置图片素材（模板里写 asset:文件名）
└─ samples/
   ├─ templates/                  示例模板，开发环境首次建库时导入为已发布版本
   └─ requests/                   示例请求体（UTF-8）
```

## 常用命令

在仓库根目录执行：

```powershell
dotnet build backend/LabelService.Backend.slnx
dotnet format backend/LabelService.Backend.slnx --verify-no-changes --no-restore
dotnet run --project backend/tests/LabelService.Backend.Checks

# 启动服务：http://localhost:5080，开发 Key lbl_test_local_dev
dotnet run --project backend/src/LabelService.Server

# 生成一张箱标
curl.exe -o box.png -H "X-Api-Key: lbl_test_local_dev" -H "Content-Type: application/json" --data-binary "@backend/samples/requests/box-label.render.json" http://localhost:5080/api/v1/render

# 渲染基准（必须 Release）
dotnet run -c Release --project backend/tests/LabelService.Benchmarks -- --filter "*"
```

## 要点

- 依赖方向：Contracts ← Rendering ← Server。Contracts 是契约，改动规则见根目录 AGENTS.md 的“契约变更”。
- 渲染是热路径：不访问数据库、磁盘、网络，单张 P95 ≤ 100 ms，见 `docs/performance.md`。
- 数据库用 SQLite，单实例部署（`docs/adr/0003-sqlite-single-instance.md`）。
- 文本渲染需要字体：先按 `assets/fonts/README.md` 把思源黑体（或同款的 Noto Sans SC）放进 `assets/fonts/`，否则文本检查不通过、中文显示成方框。
- 后端检查程序会按文本读取 `client/` 和 `frontend/` 的源码，核对错误码、HTTP 头、元素类型、示例模板是否一致。改了契约，三个部分要一起改。

## 模板存储

- **数据目录**：配置 `LabelService:Data:Directory`，默认程序目录下的 `data/`（本地 `dotnet run` 时在 `backend/src/LabelService.Server/bin/Debug/net10.0/data/`），库文件 `label.db`。启动时自动建库、执行迁移。
- **示例模板**：开发环境首次建库（库里没有模板）时，把 `samples/templates` 导入为已发布版本 1，走正常的保存草稿和发布流程。改了示例模板想重新导入，停掉服务、删掉数据目录再启动。
- **模板来源**：配置 `LabelService:Templates:Source`，默认 `Database`；设成 `Files` 时直接读示例目录、不建库，只适合只读演示。
- **草稿和发布**：`Templates/TemplateRepository.cs`（保存草稿、发布、放弃草稿、回滚到旧版本），管理接口（T-014）调它。同一模板最多一个草稿，草稿的版本号是“最新发布版本 + 1”；发布前做静态检查并用样例值试渲染计时，通过后立即生效；已发布版本不能修改、不能删除。
- **开放接口**只读 `Templates/DbTemplateStore.cs` 的内存缓存（启动时整体加载，发布时追加），不访问数据库。
- **改表结构**：改 `Server/Data/Records.cs`、`LabelDbContext.cs` 后生成迁移，不要手改迁移文件和快照。检查程序会发现漏生成的迁移。

```powershell
dotnet tool install --global dotnet-ef        # 第一次用
dotnet ef migrations add 迁移名 --project backend/src/LabelService.Server --output-dir Data/Migrations
dotnet format backend/LabelService.Backend.slnx
```
