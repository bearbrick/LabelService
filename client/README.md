# 调用端（client）

给业务系统和第三方用的 .NET SDK，以及 WinForms 调用演示程序。解决方案 `LabelService.Client.slnx`。只通过 HTTP 调用后端，不引用后端的任何项目。

```
client/
├─ LabelService.Client.slnx
├─ Directory.Build.props          调用端全部项目的编译设置
├─ Directory.Packages.props       调用端的 NuGet 版本（只有 System.Text.Json）
├─ build/pack.ps1                打包：nupkg（含 snupkg）和 zip，可选强名称和 Authenticode 签名
├─ src/
│  └─ LabelService.Client/        SDK：net462、netstandard2.0、net8.0；包里的 README.md、CHANGELOG.md 也在这里
├─ samples/
│  └─ LabelService.Demo.WinForms/ 演示程序：net48、net10.0-windows
└─ tests/
   └─ LabelService.Client.Checks/ 检查程序：对假服务端跑全部用例；有真实服务时再跑端到端
```

## 常用命令

在仓库根目录执行：

```powershell
dotnet build client/LabelService.Client.slnx
dotnet format client/LabelService.Client.slnx --verify-no-changes --no-restore
dotnet run --project client/tests/LabelService.Client.Checks

# 对正在运行的服务跑端到端检查（先启动后端服务）
$env:LABEL_SERVICE_URL = "http://localhost:5080/"; dotnet run --project client/tests/LabelService.Client.Checks

# 运行演示程序（需要后端服务在运行）
dotnet run --project client/samples/LabelService.Demo.WinForms -f net10.0-windows
```

根目录的 `verify.ps1` 会自动启动后端服务并跑端到端检查。

## 用法

```csharp
var client = new LabelServiceClient(new LabelServiceClientOptions
{
    BaseAddress = new Uri("https://label.example.com/"),
    ApiKey = ConfigurationManager.AppSettings["LabelApiKey"],
});

var file = await client.RenderAsync(new RenderRequest("BOX_LABEL", LabelFormat.Pdf)
    .Set("partName", "电源适配器")
    .Set("prodDate", DateTime.Today)
    .Set("boxNo", "C2610080001"));
file.Save(Path.Combine(@"D:\Labels", file.FileName));
```

完整的公开 API、重试和错误映射见设计书第 10 节和 `docs/contracts.md`；给第三方看的说明（快速开始、错误码、常见问题）在 `src/LabelService.Client/README.md`，会打进 NuGet 包。

## 发布

```powershell
# 在仓库根目录。输出到 artifacts/client：LabelService.Client.{版本}.nupkg、.snupkg、LabelService.Client-{版本}.zip
powershell -NoProfile -ExecutionPolicy Bypass -File .\client\build\pack.ps1
# 正式发布：指定版本、强名称密钥、代码签名证书（密码放环境变量 LABELSERVICE_CERT_PASSWORD）
powershell -NoProfile -ExecutionPolicy Bypass -File .\client\build\pack.ps1 -Version 1.0.0 -StrongNameKey D:\keys\LabelService.snk -CertificatePath D:\keys\codesign.pfx
```

- 版本：调用端独立于后端发版，语义化版本，写在 `client/Directory.Build.props`（`VersionPrefix` + 预览版的 `VersionSuffix`，如 `1.0.0-preview.1`）。改了公开 API 同步设计书第 10 节和 `CHANGELOG.md`。
- 签名：强名称密钥（.snk）和代码签名证书（.pfx）都不进仓库；没给时脚本照样打包，只是不签名（会提示）。证书由谁申请见设计书第 12 节的待定问题。
- 脚本最后核对 nupkg 里有三个目标框架的 DLL、XML 注释和 README。zip 里还有演示程序源码和依赖说明。

## 要点

- SDK 必须能编译到 net462：不用 record、init、required 等新语法，不加 Newtonsoft.Json 或日志框架依赖（检查程序会拦）。
- 错误码 `LabelErrorCodes` 和 HTTP 头 `Internal/Protocol.cs` 是后端契约的副本，后端检查程序会核对一致。
- 公开 API 变化要同步设计书第 10 节，遵守语义化版本。
