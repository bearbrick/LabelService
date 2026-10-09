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
└─ samples/
   ├─ templates/                  示例模板，开发期服务启动时加载
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
- 后端检查程序会按文本读取 `client/` 和 `frontend/` 的源码，核对错误码、HTTP 头、元素类型、示例模板是否一致。改了契约，三个部分要一起改。
