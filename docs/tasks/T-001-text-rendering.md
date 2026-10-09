# T-001 文本元素渲染与字体

状态：待认领
负责人：—
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

## 验收标准

- [ ] 示例模板的 6 个文本元素都能画出，中文正常显示
- [ ] `fit: none` 单行裁切；`wrap` 中文按字、英文按单词换行，超出框高裁掉；`shrink` 缩到 `minFontSize` 为止，仍放不下裁掉并给 `TEXT_CLIPPED` 警告
- [ ] 对齐（左中右 × 上中下）、行高、字间距生效
- [ ] 不存在的字体降级并给出 `FONT_FALLBACK:原字体名` 警告
- [ ] 字体、SKFont、SKPaint 不在每次请求时创建（缓存线程安全）
- [ ] RenderChecks 增加：文本落在框内、shrink 后宽度不超框、旋转 90° 的文本
- [ ] 基准结果写进 docs/performance.md 的“当前基线”，单张箱标 PNG 300dpi 热请求 total 仍明显低于 100 ms
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 富文本（一个元素里多种样式）
- 设计器端的字体加载（见 T-016）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
