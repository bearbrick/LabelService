# T-012 WinForms 演示程序完整版

状态：待验收
负责人：Claude 2026-10-10
里程碑：M2
依赖：T-011（SDK 公开 API 稳定）
设计书：§11 调用演示程序；原型 docs/prototypes/label-sdk-demo-prototype.html

## 目标

按原型实现 LabelServiceDemo：连接设置、按字段生成录入表格、单张和批量、预览、发送到打印机、调用代码、调用日志、内置模拟服务。

## 范围

可以新建、修改的文件：

- `client/samples/LabelService.Demo.WinForms/` 下全部文件（现在只有最小窗体 `MainForm.cs`，可以整体重写）
- 新建 `client/samples/LabelService.Demo.WinForms/Mock/MockLabelServiceHandler.cs`：不连服务器也能演示，可模拟 503 抖动

## 共享文件

- `AGENTS.md`：第 1 节“当前阶段”补一句（同时补上漏写的 T-011），单独提交

## 验收标准

- [ ] 设计书第 11 节的 5 条验收标准全部满足（4 条已验证；“只装 .NET Framework 4.8 的 Windows 10”和“斑马打印机打出来、条码能扫”要真机，见交接记录）
- [x] net48 和 net10.0-windows 两个版本都能运行，界面一致
- [x] 调用日志显示总耗时、服务端耗时、重试次数、requestId、traceId；单张超过 100 ms 标为慢调用
- [x] “调用代码”页签生成的代码在新建的 .NET Framework 4.8 和 .NET 10 项目里能编译
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- PDF 本地渲染打印（交给系统默认程序）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

### 2026-10-10 · Claude

**做了什么**

- 按原型整体重写演示程序，文件按职责拆开（都在 350 行以内）：`MainForm.cs` 布局，`MainForm.Calls.cs` 调 SDK 和调用日志，`MainForm.Results.cs` 预览和响应信息，`ZplPreview.cs` 把 `^GFA`（含 ASCII 压缩）还原成黑白图，`CodeGenerator.cs` 生成代码，`PrintForm.cs` 发送到打印机，`MainForm.Smoke.cs` 自检。
- 内置模拟服务 `Mock/`：BOX_LABEL、A4_SHIPPING_MARK 两个模板，用 System.Drawing 画示意标签，输出 PNG、PDF（每页一张 JPEG）、ZPL（不压缩的 `^GFA`）；Key 不以 `lbl_` 开头返回 401，模板不存在返回 404，缺必填返回 400 和逐条错误；勾选“模拟网络抖动”时每个请求第一次返回 503，看 SDK 自动重试。响应头和真实服务一样（X-Template-Version、X-Label-Count、X-Label-Dpi、X-Trace-Id、Server-Timing）。
- 客户端按设置缓存，设置不变就复用同一个 `LabelServiceClient`（SDK 要求单例）。
- 批量超量由 SDK 本地拦下（设计书第 10 节：抛 ArgumentException、不发请求），响应信息里加一行“对应服务端错误码 BATCH_TOO_LARGE”，和第 8 节对得上。
- 自检 `LabelServiceDemo.exe --smoke 输出目录 [服务地址]`：读取字段 → PNG → PDF → 批量 5 条 ZPL（第 5 张箱号递增到 C2610080005）→ 缺必填、Key 无效、模板未发布、批量 600 条 → A4 慢调用标记 → 调用代码，每步截图，写 `smoke.txt` 和两种写法的 `Program.*.cs.txt`。自检时 `Application.ThreadException` 直接记录并退出，不弹对话框卡住。

**验证**

- 自检 4 种组合全部通过：net48 / net10.0-windows × 内置模拟服务 / 真实服务（Debug 版服务端，临时数据目录）。真实服务的 ZPL 是压缩过的 `^GFA`，预览能正确还原（条码、二维码、图标都在）。
- 生成的代码（单张、批量）放进新建的 net48（`LangVersion` 7.3、Nullable 关）和 net10.0（Nullable 开）控制台项目，`TreatWarningsAsErrors` 下 0 警告编译通过，设好 `LABEL_API_KEY` 对真实服务运行，都生成了文件。
- `dotnet format --verify-no-changes`、完整 `verify.ps1` 通过。

**没做 / 要真机的**

- 设计书第 11 节第 1 条“只装 .NET Framework 4.8 的 Windows 10 上双击能打开”：本机是 Windows 11 + 各版本 SDK，net48 版能运行，但没在干净的 Win10 上试。
- 第 2 条“ZPL 通过 9100 在斑马打印机上打出来、条码能扫”：没有打印机。`PrintForm` 网络打印走 SDK 的 `NetworkPrinter`，USB 打印机走 `WindowsRawPrinter`，PNG 走 PrintDocument 按 DPI 原尺寸，都只编译和看过界面，没有实际打印。
- 自检要桌面会话，没放进 `verify.ps1`；改了演示程序请手动跑（命令见 `client/README.md`）。

**坑**

- 换结果时要先释放引用旧图片的 PictureBox 再释放图片，否则重绘时 GDI+ 抛 “Parameter is not valid”，WinForms 弹出未处理异常对话框，自检就卡住了。`ClearControls` 统一处理（`Controls.Clear()` 只移除不释放）。
- `ArgumentException.Message` 在 .NET Framework 会另起一行附上“参数名: x”，状态栏会被撑成两行，显示前去掉（`PlainMessage`）。
- 文本框第一次获得焦点时会全选，生成代码后显式 `Select(0, 0)`。
- 真实服务的 A4 唛头 PNG 在 Debug 版服务端单张约 220 ms，会被标成慢调用；Release 和基准结果见 T-015。

**留给下一位**

- 原型里“调用日志”的备注列（慢调用、重试说明）在最右边，窗口不够宽时要横向滚动才看得到；慢调用那一行整行标黄，不影响判断。
