# T-041 调用代码的打印写简单，PDF 加直接发文件的写法

状态：待验收
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-040
设计书：§demo

## 目标

用户看了 T-040 的调用代码，问“打印这么麻烦吗？不能直接 document.Print、把文件流给它吗？PDF 和 PNG 是不是都可以这样”。PrintDocument 没有收文件流的方法，要在 PrintPage 里画；PDF .NET 自己读不了。能精简的部分去掉：PNG 只保留“按像素 ÷ DPI 画成实际尺寸”这一行关键的（不缩放，条码才扫得出），纸张大小交给打印机驱动设置；PDF 加上“打印机支持 PDF 直接打印时，像 ZPL 一样把文件直接发过去”的写法。

## 范围

可以新建、修改的文件：

- `client/samples/LabelService.Demo.WinForms/CodeGenerator.Printing.cs`、`MainForm.Smoke.cs`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行

## 验收标准

- [x] PNG 打印方法精简到一个 PrintImage（读图、选打印机、按 DPI 画、打印），单张一行调用，批量 ZIP 逐张调用
- [x] PDF 方式一交给 PDF 阅读器，方式二（注释）直接发文件给支持 PDF 直接打印的打印机
- [x] 六份调用代码在 net48、net10.0-windows 控制台项目（C# 7.3）编译通过，0 警告
- [x] 自检通过；`verify.ps1 -Part client` 通过

## 不做

- 把 PNG 打印做进 SDK（要引入 System.Drawing.Common 依赖，Linux 上用不了；等用户决定 SDK 的目标框架时一起考虑）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 完成，待验收。PNG：去掉纸张大小、页边距、DPI 兜底和 Rectangle 换算，DrawImage 直接用 1/100 英寸的浮点宽高；演示程序自己的“发送到打印机”对话框不变（仍然把纸张设成标签大小，方便在普通打印机上试）。PDF：注释写法用全名 `LabelService.Client.Printing.WindowsRawPrinter`，不多加 using。自检（模拟服务）通过，六份代码编译 0 警告 0 错误。共享文件：任务索引追加一行。
