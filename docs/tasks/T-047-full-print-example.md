# T-047 调用代码写全三种格式的打印：PDF、多页、ZPL

状态：待验收
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-045
设计书：§demo

## 目标

用户看到调用代码里只有 PNG 的打印，问“PDF 怎么打？多页又怎么打，例子写全呀。ZPL 怎么用”。原因是调用代码只按左边选的格式写那一种打法。改成：不管选哪种格式、单张还是批量，打印部分都写全三种格式，`PrintAsync(file)` 按生成结果的格式自动选打法；一次生成多张（份数、批量、A4 拼版多页）时每种格式怎么打写在注释里：

- ZPL：多张都在一个文件里（每张一段 `^XA…^XZ`，份数是 `^PQ`），整个发一次；DPI 要和打印机一致。填了 USB 打印机名称就按 RAW 写入 Windows 打印队列，没填就发网络打印机（TCP 9100）。
- PNG：多张是 ZIP，解开后放在同一个打印任务里，一张一页（`HasMorePages`），打到哪张才读哪张。
- PDF：多页是一个文件，整份打。填了“支持 PDF 直接打印的打印机”就直接发内存里的内容；没填就交给 PDF 阅读器（填了 Windows 打印机名称时用 `printto` 指定打印机）。
- ZIP（PNG 多张、批量选了 `BatchOutput.Zip`）统一由 `Files(file)` 解开，逐个打。

打印机地址、名称都是类开头的常量，改常量就换打法，不用注释、反注释代码。请求部分加两行注释：ZPL 没指定 DPI 时提示 `Dpi` 要和打印机一致，PDF、PNG 提示可以用 `Layout` 拼版。

## 范围

可以新建、修改的文件：

- `client/samples/LabelService.Demo.WinForms/CodeGenerator.cs`、`CodeGenerator.Printing.cs`、`MainForm.Smoke.cs`、`MainForm.Layout.cs`（调用代码页签的提示，4.6.2+ 改成 4.6.1+）

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行

## 验收标准

- [x] 三种格式、单张和批量共六份调用代码，每份都有 ZPL（网络、USB）、PNG（多张一个打印任务）、PDF（直接打印、交给阅读器）和解 ZIP 的写法
- [x] 六份代码在 net48、net10.0-windows 控制台项目编译 0 警告
- [x] 接本机服务实际运行：ZPL 单张、批量发到假的网络打印机（批量 3 张一次发完，3 段 `^XA`）；PNG 批量用打印预览确认 3 张是一个任务 3 页（net48、net10 都试了）；打印机名称不对时进 catch
- [x] 自检通过，`verify.ps1 -Part client` 通过

## 不做

- 在程序里把 PDF 渲染成图片再打印（要另加 PDF 库，生成的代码不引入 NuGet 包）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 完成，待验收。打印部分不再随格式变化，所有调用代码都要引用 System.Drawing、System.IO.Compression（.NET 8 要 net8.0-windows + UseWindowsForms），文件开头写了怎么配、只打 ZPL/PDF 时删哪里。打印出错的 catch 合并成一个（SocketException、TimeoutException、Win32Exception、InvalidPrinterException），顺手把注释里过时的“标签文件已经保存”改掉了（T-045 起不先存文件）。实测时用 PreviewPrintController 代替真实打印，没有往本机打印机发任务。PDF 交给阅读器打印要装 Adobe Reader、福昕这类支持打印命令的阅读器，没在本机实测。共享文件：任务索引追加一行。
