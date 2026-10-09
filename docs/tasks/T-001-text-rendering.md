# T-001 文本元素渲染与字体

状态：待验收
负责人：Claude 2026-10-09
里程碑：M2
依赖：—
设计书：§5 文本排版规则、§7 字体

## 目标

服务端能按规范画出 `text` 元素：字体、字号、粗斜体、下划线、颜色、对齐、`fit`（none / wrap / shrink）、行高、字间距，和设计器的显示一致。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Rendering/Elements/TextPainter.cs`（以及需要的 `TextLayout` 等辅助类，同目录）
- 新建 `backend/src/LabelService.Rendering/Fonts/`：字体注册表，启动时从目录加载，按 fontFamily 名称匹配，找不到降级到思源黑体并加 `FONT_FALLBACK` 警告
- 新建 `backend/assets/fonts/`：思源黑体、思源宋体（常规、粗体）和 `OFL.txt`；文件较大，提交前确认仓库是否启用 Git LFS
- `backend/tests/LabelService.Backend.Checks/RenderChecks.cs`：补文本相关检查

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `backend/src/LabelService.Rendering/Elements/ElementPainters.cs`：登记 `TextPainter`
- `backend/src/LabelService.Server/Program.cs`、`LabelService.Server.csproj`：注册字体目录、复制字体到输出目录
- `backend/src/LabelService.Rendering/Elements/RenderContext.cs`：如需传入字体注册表
- `docs/contracts.md`：元素表的 text 改为已实现；补 minFontSize 默认值和文本排版规则
- `AGENTS.md`：第 1 节当前阶段、第 2 节仓库地图加字体目录、第 9 节字体的坑
- `.gitignore`、`.gitattributes`：忽略字体文件（存放方式待定）；字体按二进制处理

## 验收标准

- [x] 示例模板的 6 个文本元素都能画出，中文正常显示
- [x] `fit: none` 单行裁切；`wrap` 中文按字、英文按单词换行，超出框高裁掉；`shrink` 缩到 `minFontSize` 为止，仍放不下裁掉并给 `TEXT_CLIPPED` 警告
- [x] 对齐（左中右 × 上中下）、行高、字间距生效
- [x] 不存在的字体降级并给出 `FONT_FALLBACK:原字体名` 警告
- [x] 字体、SKFont、SKPaint 不在每次请求时创建（缓存线程安全）
- [x] RenderChecks 增加：文本落在框内、shrink 后宽度不超框、旋转 90° 的文本
- [x] 基准结果写进 docs/performance.md 的“当前基线”，单张箱标 PNG 300dpi 热请求 total 仍明显低于 100 ms
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 富文本（一个元素里多种样式）
- 设计器端的字体加载（见 T-016）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收。
  - 做了什么：`Rendering/Fonts/`（`FontRegistry` 启动时加载字体目录，按名字匹配，思源和 Noto 同款互认，缺失时降级并给 FONT_FALLBACK；`FontFace` 缓存字形和参考字号的 SKFont）；`Elements/TextLayout.cs`（测量、换行、shrink、对齐、行高、字间距，纯计算）；`Elements/TextPainter.cs`（裁切、下划线、TEXT_CLIPPED）；Server 加 `Fonts/FontSetup.cs`（配置 `LabelService:Fonts:Directory`，启动时加载并写日志），csproj 把 `backend/assets/fonts` 复制到输出目录的 `fonts/`。检查程序新增 `TextChecks.cs`（31 项：字体匹配和降级、各种排版、像素）、`ContractChecks` 新增“文档里的已实现和登记的绘制器一致”、`ServerChecks` 新增“示例模板没有字体降级”。基准改为加载真实字体。
  - 排版规则（已写进 docs/contracts.md）：内容里的换行符三种模式都换行；wrap 中文按字、英文按单词，行首不放结束标点、行尾不放开始标点，单词比框宽时按字符断；shrink 二分 14 次、下限 max(minFontSize 默认 4pt, 1mm)，和设计器一致；每行文字按 ascent、descent 在行高内居中（等同浏览器 middle 基线）；下划线位置和粗细照 Konva。
  - 实现要点：每个字体只按参考字号 100 建两个 SKFont（抗锯齿、黑白），排版时字宽按比例换算，绘制时缩放画布，所以 shrink 不重复测量、渲染时不创建字体；画笔每线程一支。可变字体（Noto Sans SC 默认字重是最细的 100）按 400、700 生成实例；没有粗体或斜体字形时合成。
  - 基准（同一台机器，`--job short`，单张箱标 PNG）：改动前 203dpi 5.22 ms、300dpi 11.02 ms；改动后 5.65 ms、11.91 ms（+8%，6 个文本元素），分配从 10–17 KB 增加到 49–66 KB。真实服务热请求 total 中位数 11.4 ms；第一次请求 62 ms（layout 20 ms，首次栅格化字形），留给 T-017 预热。已写进 docs/performance.md。
  - **需要人决定**：字体文件几十 MB（思源 OTF 四个文件约 40 MB，Noto 可变字体两个文件约 43 MB），这次没有提交，`.gitignore` 忽略了字体文件。可选：① 用 Git LFS 提交（本机已装 git-lfs，GitHub 免费额度每月 1 GB 流量）；② 写下载脚本，从 Adobe / Google 官方 Release 下载并校验 SHA-256；③ 直接提交进仓库。建议 ②。本机开发暂时把系统里已装的 Noto Sans SC、Noto Serif SC 可变字体复制到了 `backend/assets/fonts/`（没提交）。字体包里的授权文件 OFL.txt 也要随字体一起放。
  - 没做 / 已知差异：没有逐字回退（字体里没有的字显示成方框）；没有 HarfBuzz 排版（没有连字、字距调整，阿拉伯文、泰文等不支持，中英文不受影响）；合成粗体的粗细和浏览器在小字号时略有差别（只在没有粗体字形时出现）。设计器（Konva）的 word 换行在“中英混排且有空格”时会退到最后一个空格断行，和这里的“中文按字”不完全一致，T-016 做位置对比时建议把 `TextLayout` 的断行规则移植到设计器。
  - 留给下一位：T-002 条码下面的文字可以直接用 `TextLayout.Arrange` + `FontRegistry.Resolve`；T-005 PDF 绘制时同样用 `FontFace.Font(...)`，Skia 的 PDF 后端会嵌入字体子集。
- 2026-10-09 · Claude · 认领。计划：字体注册表启动时加载 fonts/ 目录，模板字体名同时认思源和 Noto 的名字（同一款字体）；文本按参考字号测量一次、按比例缩放，shrink 二分查找不重复测量；换行中文按字、英文按单词，行首不放结束标点。共享文件：ElementPainters.cs（登记一行）、RenderContext.cs（加字体注册表）、Program.cs 和 Server.csproj（字体目录）。
