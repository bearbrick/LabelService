# 0014 可选 Windows 打印模块使用系统 PDF 引擎

状态：已采纳（用户 2026-10-12 要求 PNG、PDF、ZPL 全部支持静默打印，PDF 不依赖阅读器，授权替换演示代码）
日期：2026-10-12
关联：T-048、ADR 0013

## 背景

客户通常使用实施人员预先配置的 USB 打印机。原示例把不同格式的底层方法、网络地址、RAW、ZIP 和阅读器调用全部展开，客户需要理解过多细节。用户要求常规打印只有一个入口，特殊情况通过标准参数调整。

普通 Windows 驱动不能直接接收任意 PDF 字节。PDF 必须先由渲染引擎转换成驱动能够绘制的页面，或由明确支持 PDF Direct 的设备解释。不能把 RAW 写入成功等同于通用 PDF 打印成功。

## 决定

- 增加独立可选模块 `LabelService.Client.Windows`，目标 net461、net8.0-windows。HTTP SDK 的目标和零 NuGet 依赖保持不变。
- `WindowsLabelPrinter.Print(file, printerName)` 支持 PNG、PDF、ZPL 和相应的 ZIP。不传名称时使用 Windows 默认打印机，默认保留驱动配置，不弹选择框或 .NET 进度框。
- PNG、PDF 的特殊情况通过 `CreateDocument` 取得标准 PrintDocument，调用方先改本次设置再打印。PDF 每页通过 Windows 自带 Windows.Data.Pdf 渲染，要求 Windows 8.1+，不安装阅读器、不新增第三方库。
- 原生 ABI 桥接放在内部，使用专用 MTA 线程并成对释放所有接口，避免 .NET Framework 与现代 .NET 的 WinRT 投影差异及界面线程等待。真实 PDF、两套运行时和服务端 PDF 都做回归。
- ZPL 使用原 SDK 的 RAW 辅助方法；只有支持 ZPL 的设备可以使用。份数、DPI 在生成请求设置，网络发送辅助方法保留。
- 打印模块随 SDK 打包为独立 nupkg，并在 zip 的 windows/ 目录提供 DLL 和说明。

## 备选方案

- 外部阅读器：用户明确不需要，且静默能力取决于阅读器注册和命令行。
- 第三方 PDF 库：需要新增和分发渲染器，不符合当前尽量少依赖的目标。
- PDF Direct：仅部分设备支持，无法作为普通 USB 激光打印机的通用入口。
- 把 Windows 打印放进原 SDK：破坏跨平台和零依赖边界，因此使用可选模块。

## 影响

PDF 栅格化后打印，按原页面准确物理尺寸绘制；渲染 DPI 使用生成结果的 DPI。硬件不可打印区域仍需模板留白。每次按需处理一页，PDF 文档只加载一次，不写临时文件。只适用于桌面客户端，不支持在 ASP.NET 或 Windows 服务中使用此打印 API。

演示程序默认静默打印到系统默认打印机，特殊情况下仍可选择其他已安装打印机。原型和设计书仅同步这一已授权行为。真实出纸、扫码、驱动纸张、切纸等留给客户设备验收。

依据：[微软 PDF 加载接口](https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfdocument.loadfromstreamasync)、[微软随机访问流桥接](https://learn.microsoft.com/en-us/windows/win32/api/shcore/nf-shcore-createrandomaccessstreamoverstream)、[微软生成的 ABI 定义](https://github.com/microsoft/windows-rs/blob/master/crates/libs/windows/src/Windows/Data/Pdf/mod.rs)。
