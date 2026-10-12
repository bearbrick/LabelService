# T-042 SDK 正式支持 .NET Framework 4.6.1，去掉 System.Text.Json 依赖

状态：待验收
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-034
设计书：§sdk

## 目标

用户的客户里有 .NET Framework 4.6.1 的老系统，要求 SDK 正式支持（ADR 0013）。SDK 原来最低 4.6.2，卡在 System.Text.Json 8.x 只支持 4.6.2 起。改成 SDK 自带小 JSON 读写，不依赖任何 NuGet 包，目标框架 `net461`、`netstandard2.0`、`net8.0`。

## 范围

可以新建、修改的文件：

- `client/src/LabelService.Client/`（新建 `Internal/JsonText.cs`、`Internal/Responses.cs`；`Payloads.cs`、`LabelServiceClient.cs`、`TemplateSchema.cs`、项目文件、README、CHANGELOG）
- `client/tests/LabelService.Client.Checks/`（新建 `JsonChecks.cs`；架构检查改成 net461、不依赖 NuGet 包）
- `client/samples/LabelService.Demo.WinForms/`（样例值的显示、关于对话框、net48 版直接引用 System.Text.Json、自检里一处等待）
- `client/build/pack.ps1`、`client/README.md`、ADR 0013、设计书第 10 节

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `AGENTS.md`：仓库地图一行、R4
- `client/src/LabelService.Client/Internal/Protocol.cs`：删掉 `ClientJson`（只剩协议常量和错误响应体）
- `client/Directory.Packages.props`：分组说明（System.Text.Json 现在只给演示程序用）
- `docs/tasks/README.md`、`docs/adr/README.md`

## 验收标准

- [x] SDK 目标框架 net461、netstandard2.0、net8.0，nupkg 里三个 lib 都没有依赖，net461 只引用框架自带的 System.Net.Http
- [x] 新建的 net461 控制台项目引用 SDK：0 警告，输出目录只有 LabelService.Client.dll，能列模板、读字段、生成标签（本机运行时是 4.8）
- [x] JSON 读写和响应映射有检查：转义、中文、数字、嵌套、格式错误、属性名大小写、错误响应
- [x] 原有调用端检查（请求体格式、错误映射、重试等）和对真实服务的端到端检查都通过
- [x] 演示程序 net48、net10.0-windows 自检通过
- [x] `verify.ps1` 通过
- [x] 相关文档已更新（ADR 0013、AGENTS.md R4、设计书第 10 节、README、CHANGELOG）

## 不做

- .NET Framework 4.0、4.5（4.0 没有 HttpClient 和 async/await；4.5 以后要支持时 JSON 已经不是障碍，加一个 net45 目标即可）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 完成，待验收。
  - JSON：`JsonText` 写（字典按添加顺序、字符串转义控制字符、中文原样、double 用 R 格式、NaN 报错）和读（递归下降，对象 → Dictionary，数组 → List，数字 → decimal、超出时 double，最多嵌套 64 层，开头可以有 BOM，错误抛 FormatException）；`Responses` 把读出的对象换成字段定义、模板列表、错误响应体，属性名先精确后忽略大小写（和原来 System.Text.Json 的设置一样）。请求体和原来逐字节一样（原有检查里对请求体的断言都没改，照样通过）。
  - 公开 API 不兼容的地方：`TemplateSchema.Sample` → `Dictionary<string, object?>`，`TemplateField.Default` → `object?`；`Set` 传 JsonElement 改成按文本发送。都写进了还没发布的 1.0.0-preview.3 的 CHANGELOG。
  - 演示程序：样例值用 `System.Convert.ToString` 显示；net48 版的内置模拟服务还要用 System.Text.Json，原来是跟着 SDK 间接带进来的，现在演示程序自己引用（版本沿用 Directory.Packages.props 里的 8.0.6，不是新包）。顺手修了自检里一处偶发失败：“模拟网络抖动”那一项在调用日志异步加进列表之前就去看了，.NET 4.8 冷启动时偶尔读早了；改成先等这条日志出现（最多 1 秒）。修完 net48 连跑三次、net10 一次都通过。
  - 验证：调用端检查 65 项（新增 JSON 9 项、架构 1 项），带 LABEL_SERVICE_URL 跑真实服务全部通过；net461 探针项目 0 警告、能调通；`dotnet pack` 出的 nuspec 三个依赖组都是空的。只装 4.6.1 运行时的机器本机没有，没有实测（本机是 4.8，.NET 4.x 是原地升级）。
  - 提交说明：共享文件 Protocol.cs 单独提交（f915e0e）时，其余 SDK 文件还在下一个提交里，所以 f915e0e 单独检出不能编译；改写分支历史要用户确认，没有动。`verify.ps1` 全量通过：后端 517、调用端 65、前端 104。
