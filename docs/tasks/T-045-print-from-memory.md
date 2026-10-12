# T-045 调用代码直接用内存里的结果打印

状态：待验收
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-041
设计书：§demo

## 目标

用户问“不能直接打印文件流吗？非要保存成文件再打印吗？”。其实 ZPL、PNG 的打印本来就是用内存里的内容（`file.Content`），只是 Main 里在打印前面先 `file.Save(path)` 留了一份底，看着像“先存再打”。改成：打印直接用内存里的结果，另存一份写成注释；只有 PDF 交给 PDF 阅读器时才存临时文件（阅读器只能打开磁盘上的文件），写在 PDF 的打印方法里并说明原因；PDF 直接发给打印机的写法改成发内存里的内容。

## 范围

可以新建、修改的文件：

- `client/samples/LabelService.Demo.WinForms/CodeGenerator.cs`、`CodeGenerator.Printing.cs`、`MainForm.Smoke.cs`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行

## 验收标准

- [x] ZPL、PNG 的调用代码不先存文件，直接用 `file.Content` 打印；另存一份是注释
- [x] PDF 的临时文件在打印方法里，注释说明为什么要存；方式二直接发 `file.Content`
- [x] 自检通过；六份调用代码在 net48、net10.0-windows 控制台项目编译 0 警告
- [x] `verify.ps1 -Part client` 通过

## 不做

- 无

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 完成，待验收。PDF 的 `Print` 改成收 `LabelFile`（三种格式的调用都是 `Print(file)` / `PrintAsync(file)`）。自检加了一项“不先存文件”。共享文件：任务索引追加一行。
