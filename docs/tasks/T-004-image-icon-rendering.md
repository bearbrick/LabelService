# T-004 图片和图标渲染

状态：待认领
负责人：—
里程碑：M2
依赖：—
设计书：§4 元素类型（图片、图标）、§9 其他非功能需求（安全）

## 目标

服务端能画出 `image`（来自 image 字段或素材库）和 `icon`（内置 GB/T 191 图标库或上传的 SVG）。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Rendering/Elements/ImagePainter.cs`、`IconPainter.cs`
- 新建 `backend/src/LabelService.Rendering/Assets/`：素材和图标的内存注册表（启动时加载，按 ID / 路径查找）
- 新建 `backend/assets/icons/gb191/`：GB/T 191 包装储运图示标志 SVG（注意来源和授权，自己绘制最稳妥）
- `backend/tests/LabelService.Backend.Checks/RenderChecks.cs`

## 共享文件

- `backend/src/LabelService.Rendering/Elements/ElementPainters.cs`
- `backend/src/LabelService.Rendering/LabelService.Rendering.csproj`：引用 `Svg.Skia`（版本已在 backend/Directory.Packages.props）

## 验收标准

- [ ] `fit` 三种方式正确；`mono: true` 时抖动成黑白
- [ ] `src` 为 `{{字段}}` 时取 image 字段的 byte[]；为 `asset:ID` 时取素材；找不到素材抛 `IMAGE_INVALID`
- [ ] 解码后的图片按目标尺寸缩放后缓存（同一素材同一尺寸只缩放一次）
- [ ] SVG 图标按 `color` 着色；SVG 里的脚本、外部引用不执行
- [ ] 渲染路径上不读磁盘（素材在启动时加载）
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 素材上传和管理界面（见 T-014）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
