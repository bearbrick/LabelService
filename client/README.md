# 调用端（client）

给业务系统和第三方用的 .NET SDK，以及 WinForms 调用演示程序。解决方案 `LabelService.Client.slnx`。只通过 HTTP 调用后端，不引用后端的任何项目。

当前 SDK 和 Windows 扩展为 `1.0.0-preview.5`，正式发版仍需用户确认。集成使用 [HTTP SDK 说明](src/LabelService.Client/README.md) 与 [Windows 打印说明](src/LabelService.Client.Windows/README.md)；现场待验收项见 [定版清单](../docs/release-readiness.md)。

```
client/
├─ LabelService.Client.slnx
├─ Directory.Build.props          调用端全部项目的编译设置
├─ Directory.Packages.props       调用端的 NuGet 版本（只有演示程序 .NET Framework 版的模拟服务用 System.Text.Json，SDK 不依赖任何包）
├─ build/pack.ps1                打包：nupkg（含 snupkg）和 zip，可选强名称和 Authenticode 签名
├─ src/
│  ├─ LabelService.Client/        SDK：net461、netstandard2.0、net8.0；包里的 README.md、CHANGELOG.md 也在这里
│  └─ LabelService.Client.Windows/ 可选 Windows 静默打印：net461、net8.0-windows；PDF 使用系统引擎，不需要阅读器
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

字段 schema 新增可选 `TemplateField.Source` 和 `RequiresInput`。模板提供字段由服务生成，演示输入只读并从请求/调用代码排除；旧 schema 省略来源时保持原有行为，详见 SDK README。

```csharp
var client = new LabelServiceClient(new LabelServiceClientOptions
{
    BaseAddress = new Uri("https://label.example.com/"),
    ApiKey = Environment.GetEnvironmentVariable("LABEL_API_KEY"),
});

var file = await client.RenderAsync(new RenderRequest("BOX_LABEL", LabelFormat.Pdf)
    .Set("partName", "电源适配器")
    .Set("partNo", "3100-0123")
    .Set("qty", 50)
    .Set("batchNo", "20261008-A")
    .Set("prodDate", DateTime.Today)
    .Set("boxNo", "C2610080001"));
file.Save(Path.Combine(@"D:\Labels", file.FileName));
```

完整的公开 API、重试和错误映射见设计书第 10 节和 `docs/contracts.md`；给第三方看的说明（快速开始、错误码、常见问题）在 `src/LabelService.Client/README.md`，会打进 NuGet 包。

## Windows 静默打印

客户的 Windows 打印机由实施人员提前配置好，引用可选的 `LabelService.Client.Windows` 后，三种格式使用一个入口：

```csharp
WindowsLabelPrinter.Print(file, printerName);
WindowsLabelPrinter.Print(file); // 不传名称时使用 Windows 默认打印机
```

命名空间为 `LabelService.Client.Printing`。PNG、PDF 和相应的 ZIP 连续分页打印，保留实际尺寸，默认不覆盖驱动的纸张、方向、纸盒和份数。PDF 通过 Windows 自带引擎逐页渲染，需要 Windows 8.1+，不启动阅读器、不写临时文件。ZPL 原样提交，设备必须支持 ZPL，DPI 和份数在生成请求设置。

特殊情况下，PNG、PDF 可用 `CreateDocument(file, printerName)` 取得标准 `PrintDocument`，先修改本次参数再调用 `Print()`，使用完释放。网络 ZPL 辅助方法继续保留。扩展只依赖原 HTTP SDK 和 Windows 桌面框架，不增加第三方 NuGet 包；详细说明见 [打印扩展 README](src/LabelService.Client.Windows/README.md)。

一次生成后按项补打：`var result = await client.RenderBatchFilesAsync(batch)`，整批调用 `WindowsLabelPrinter.Print(result.Items)`，第二项调用 `Print(result.Items[1])`，子集传选中项数组。普通标签按业务条目（含该条全部份数）组织，A4 拼版按纸页组织，PDF 纸页引用原文档且不重新生成。原 API 保持可用；完整语义见 [SDK README](src/LabelService.Client/README.md)。

## 演示程序

`samples/LabelService.Demo.WinForms` 生成 `LabelServiceDemo.exe`（net48 和 net10.0-windows 两个版本，界面相同），按设计书第 11 节和原型 `docs/prototypes/label-sdk-demo-prototype.html` 实现。

EXE、主窗口和打印窗口使用同一个深蓝条码图标，内含 16～256 像素共七种尺寸，图标嵌入程序集，不需要单独部署 ICO 文件。修改图形后可以运行 `powershell -NoProfile -ExecutionPolicy Bypass -File client/build/New-DemoIcon.ps1` 重新生成。

批量生成后预览区增加“已生成项”选择框及“静默打印所选项”，上方“静默打印”提交整批；切换为单张或生成失败时清除旧集合。补打使用已经生成的内容，不受后来编辑业务字段影响。

| 文件 | 内容 |
| --- | --- |
| `MainForm.cs` | 控件定义、窗口大小（100% 缩放时默认 1240×760、最小 960×600，按屏幕缩放，不超出工作区）、组装界面 |
| `FormScaling.cs` | 窗口按屏幕缩放（`AutoScaleMode.Dpi`）：尺寸按 100% 缩放写，按钮按文字定大小 |
| `MainForm.Layout.cs` | 界面布局：连接设置、模板与数据、生成选项，右边预览、调用代码、调用日志、响应信息四个页签；窗口矮时左栏可以上下滚动，字段表格至少显示 6 行 |
| `MainForm.Calls.cs` | 调 SDK：建客户端（设置不变就复用同一个实例）、读取字段、单张和批量、调用日志（`CallCompleted` 事件） |
| `MainForm.Templates.cs` | 模板编码下拉框：`ListTemplatesAsync` 刷新，列表里显示名称和尺寸 |
| `MainForm.Batch.cs` | 批量数据：一行一张标签、每条数据给全；加行、删行、从 Excel 粘贴（带表头时按表头对列） |
| `MainForm.Results.cs` | 显示结果：PNG 预览、批量缩略图、ZPL 指令和黑白效果、PDF 可由默认程序查看；静默打印使用独立统一入口，无需阅读器 |
| `MainForm.BatchPrinting.cs` | 整批、选中项及子集打印；区分业务条目和拼版纸页，保留原文件内容 |
| `ZplPreview.cs` | 把 ZPL 的 `^GFA` 位图（含 ASCII 压缩）还原成图片 |
| `CodeGenerator.cs` | “调用代码”页签：按当前设置生成 C# 7.3 的 Program.cs，单张和批量两种写法 |
| `CodeGenerator.Printing.cs` | 调用代码里的统一静默打印入口；PNG、PDF、ZPL 共用一行，特殊参数和网络 ZPL 用法放在注释中 |
| `MainForm.Printing.cs` | 默认静默打印当前完整结果到系统默认打印机；后台提交，避免重复点击，状态栏报告提交结果 |
| `PrintForm.cs` | 特殊情况选择其他已安装打印机；使用相同入口，非 ZIP 的 ZPL 也可直接发网络打印机 |
| `MainForm.Smoke.cs` | 自检（见下） |
| `Mock/` | 内置模拟服务（`HttpMessageHandler`）：BOX_LABEL、A4_SHIPPING_MARK 两个模板，错误码和真实服务一致，可模拟首次 503 看 SDK 重试 |

自检（开发用）：走一遍界面缩放（文字不折行）、刷新模板、读取字段、三种格式、批量 ZPL、粘贴批量数据、四种错误（缺必填、Key 无效、模板未发布、批量超量）和调用代码（三种格式、单张和批量各存一份 Program.*.cs.txt，可以放进控制台项目编译），每步截图，结果写进 `smoke.txt`，全部通过时退出码 0。要有桌面会话，所以没有放进 `verify.ps1`，改了演示程序手动跑：

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

- 版本：调用端独立于后端发版，语义化版本，写在 `client/Directory.Build.props`（`VersionPrefix` + `VersionSuffix`，当前 `1.0.0-preview.5`）。改了公开 API 同步设计书第 10 节和 `CHANGELOG.md`；准备定版不自动升级为正式版。
- 签名：强名称密钥（.snk）和代码签名证书（.pfx）都不进仓库；没给时脚本照样打包，只是不签名（会提示）。证书由谁申请见设计书第 12 节的待定问题。
- 脚本最后核对 nupkg 里有三个目标框架的 DLL、XML 注释和 README。zip 里还有演示程序源码和依赖说明。
- 打包同时生成 `LabelService.Client.Windows` 的 nupkg；zip 的 `windows/` 目录包含两个 Windows 目标的扩展 DLL、XML 注释和说明。原 HTTP SDK 保持原有独立包和目标框架。

## 要点

- SDK 必须能编译到 net461（ADR 0013）：不用 record、init、required 等新语法，不依赖任何 NuGet 包，JSON 读写用自带的 `Internal/JsonText.cs`（检查程序会拦）。
- 错误码 `LabelErrorCodes` 和 HTTP 头 `Internal/Protocol.cs` 是后端契约的副本，后端检查程序会核对一致。
- 公开 API 变化要同步设计书第 10 节，遵守语义化版本。
- 注释（AGENTS.md R5）：SDK、演示程序、检查程序的每个类型和成员都写完整的 XML 注释，检查程序核对（演示程序按 net10.0-windows 的编译结果核对，要先编译解决方案；没有 Windows 桌面运行时的环境跳过演示程序）。
