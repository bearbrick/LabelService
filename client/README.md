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
dotnet run --project client/tests/LabelService.Client.Checks -- docs   # 只检查注释，列出全部缺漏

# 对正在运行的服务跑端到端检查（先启动后端服务）
$env:LABEL_SERVICE_URL = "http://localhost:5080/"; dotnet run --project client/tests/LabelService.Client.Checks

# 运行演示程序（勾选“使用内置模拟服务”时不需要后端服务）
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

## 演示程序

`samples/LabelService.Demo.WinForms` 生成 `LabelServiceDemo.exe`（net48 和 net10.0-windows 两个版本，界面相同），按设计书第 11 节和原型 `docs/prototypes/label-sdk-demo-prototype.html` 实现。

| 文件 | 内容 |
| --- | --- |
| `MainForm.cs` | 控件定义、窗口大小（默认 1240×760，最小 960×600）、组装界面 |
| `MainForm.Layout.cs` | 界面布局：连接设置、模板与数据、生成选项，右边预览、调用代码、调用日志、响应信息四个页签；窗口矮时左栏可以上下滚动，字段表格至少显示 6 行 |
| `MainForm.Calls.cs` | 调 SDK：建客户端（设置不变就复用同一个实例）、读取字段、单张和批量、调用日志（`CallCompleted` 事件） |
| `MainForm.Results.cs` | 显示结果：PNG 预览、批量缩略图、ZPL 指令和黑白效果、PDF 交给默认程序；`LabelFile` / `LabelServiceException` 的全部属性 |
| `ZplPreview.cs` | 把 ZPL 的 `^GFA` 位图（含 ASCII 压缩）还原成图片 |
| `CodeGenerator.cs` | “调用代码”页签：按当前设置生成 C# 7.3 的 Program.cs，单张和批量两种写法 |
| `PrintForm.cs` | 发送到打印机：网络打印机（`NetworkPrinter`）、Windows 打印机（ZPL 用 `WindowsRawPrinter`，PNG 用 PrintDocument 按 DPI 原尺寸） |
| `MainForm.Smoke.cs` | 自检（见下） |
| `Mock/` | 内置模拟服务（`HttpMessageHandler`）：BOX_LABEL、A4_SHIPPING_MARK 两个模板，错误码和真实服务一致，可模拟首次 503 看 SDK 重试 |

自检（开发用）：走一遍读取字段、三种格式、批量 ZPL、四种错误（缺必填、Key 无效、模板未发布、批量超量）和调用代码，每步截图，结果写进 `smoke.txt`，全部通过时退出码 0。要有桌面会话，所以没有放进 `verify.ps1`，改了演示程序手动跑：

```powershell
# 内置模拟服务
.\client\samples\LabelService.Demo.WinForms\bin\Debug\net48\LabelServiceDemo.exe --smoke D:\temp\demo-smoke
# 真实服务（先启动后端）
.\client\samples\LabelService.Demo.WinForms\bin\Debug\net10.0-windows\LabelServiceDemo.exe --smoke D:\temp\demo-live http://localhost:5080/
```

输出目录里的 `Program.Single.cs.txt`、`Program.Batch.cs.txt` 是生成的代码，放进新建的 net48（C# 7.3）或 net10 控制台项目、引用 SDK 就能编译运行。

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
- 注释（AGENTS.md R5）：SDK、演示程序、检查程序的每个类型和成员都写完整的 XML 注释，检查程序核对（演示程序按 net10.0-windows 的编译结果核对，要先编译解决方案；没有 Windows 桌面运行时的环境跳过演示程序）。
