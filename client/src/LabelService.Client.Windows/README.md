# Windows 静默打印扩展

引用 `LabelService.Client.Windows`，使用 `LabelService.Client.Printing.WindowsLabelPrinter`：

```csharp
WindowsLabelPrinter.Print(file, printerName);
WindowsLabelPrinter.Print(file); // 使用 Windows 默认打印机
// 使用 SDK 的集合接口生成后：
WindowsLabelPrinter.Print(result.Items);    // 整批
// WindowsLabelPrinter.Print(result.Items[1]); // 只打第 2 条或第 2 页，替代上面一行
// WindowsLabelPrinter.Print(new[] { result.Items[0], result.Items[2] }); // 指定子集
```

PNG、PDF、ZPL 和相应格式的 ZIP 共用入口，不需要先保存文件。PNG、PDF 按实际尺寸连续分页，默认使用驱动已经配置的纸张、方向、纸盒和份数，不弹打印机选择框或 .NET 进度框。默认按纸张原点定位；硬件不可打印区域仍需在模板中预留空白。

特殊情况，先取得标准打印文档：

```csharp
using (var document = WindowsLabelPrinter.CreateDocument(file, printerName))
{
    document.PrinterSettings.Copies = 2;
    document.Print();
}
```

`CreateDocument` 适用于 PNG、PDF。ZPL 按 RAW 发送，打印机必须支持 ZPL；份数和 DPI 在服务生成请求中设置。网络 ZPL 仍可使用原 SDK 的 `NetworkPrinter.SendAsync`。

`Print`、`CreateDocument` 都提供 `LabelBatchItem` 和 `IEnumerable<LabelBatchItem>` 重载。普通项保留该业务条目的全部生成份数，拼版项只打印对应的纸页；选择顺序即打印顺序。PDF 拼版项的文件是完整原 PDF，必须传项以保留页选择。整批图形使用一个文档连续分页、ZPL 使用一个 RAW 作业；不能把 ZPL 和 PNG/PDF 混在同一作业。空集合、损坏图形及越界 PDF 页在提交前被拒绝。

PDF 使用 Windows 自带的 `Windows.Data.Pdf` 引擎，需要 Windows 8.1 或更新版本，不安装阅读器，不引入第三方 PDF 库。PDF 逐页栅格化后通过驱动打印，物理尺寸取原 PDF 页面，不随图像取整改变。纸张较大时可降低请求 DPI。没有图形驱动的小票设备、切纸和设备专用指令不属于此模块。

系统 PDF 渲染尺寸参数采用 DIP，可能受桌面缩放影响；模块把返回位图归一到生成 DPI 对应的像素数，物理纸张尺寸始终取原 PDF。

库目标为 net461 和 net8.0-windows；现代 .NET 的桌面项目使用 Windows 目标并设置 `UseWindowsForms=true`。这是可选的 Windows 桌面组件，原 HTTP SDK 继续支持 net461、netstandard2.0、net8.0 且没有 NuGet 依赖。不用于 ASP.NET 或 Windows 服务进程。

`Print` 同步提交打印任务；返回表示提交完成，不表示打印机已经出纸。错误以异常交给业务系统处理，打印状态由 Windows 队列和设备负责。
