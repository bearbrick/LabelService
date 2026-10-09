# T-004 图片和图标渲染

状态：待验收
负责人：Claude 2026-10-09
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
- `backend/src/LabelService.Rendering/Elements/RenderContext.cs`：加素材库
- `backend/src/LabelService.Server/Program.cs`、`LabelService.Server.csproj`：注册素材库、复制图标和图片
- `docs/contracts.md`：image、icon 改为已实现，补规则
- `AGENTS.md`：第 1 节当前阶段

## 验收标准

- [x] `fit` 三种方式正确；`mono: true` 时抖动成黑白
- [x] `src` 为 `{{字段}}` 时取 image 字段的 byte[]；为 `asset:ID` 时取素材；找不到素材抛 `IMAGE_INVALID`
- [x] 解码后的图片按目标尺寸缩放后缓存（同一素材同一尺寸只缩放一次）
- [x] SVG 图标按 `color` 着色；SVG 里的脚本、外部引用不执行
- [x] 渲染路径上不读磁盘（素材在启动时加载）
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 素材上传和管理界面（见 T-014）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收。
  - 做了什么：`Rendering/Assets/`：`AssetLibrary`（启动时加载图标目录和图片目录，图片按“ID + 尺寸 + fit + mono”栅格化后缓存，上限 512 条）、`ImageRaster`（三种 fit、Floyd–Steinberg 抖动，直接读写像素缓冲区）、`SvgSanitizer`（禁 DTD；删 script、style、foreignObject、动画元素、on* 属性；href 只留 #id 和 data:image/；url() 只留 #id）。`Elements/ImagePainter.cs`、`IconPainter.cs`。Server 加 `Assets/AssetSetup.cs`（配置 `LabelService:Assets`，上传的素材在数据目录 `assets/`，同名优先于内置）。`backend/assets/icons/gb191/` 8 个图标由设计器的 `icons.ts` 生成，`backend/assets/images/` 是设计器的两个内置演示素材。
  - **安全**：实测 Svg.Skia 会按 `<image href>` 去请求外部地址（一个指向本机关闭端口的 SVG 加载要 2 秒），所以所有 SVG 一律先清洗；检查程序验证清洗后加载不访问外部。
  - 检查：新增 `AssetChecks.cs`（18 项）：清洗规则、XXE、加载不访问外部；图标画出、着色、不存在的错误码；素材 contain、缓存、mono；字段图片三种 fit（用红绿蓝黄条纹图区分）、可选字段不传、50% 灰抖动约一半黑点；`ContractChecks` 新增“设计器和服务端的图标一致”“内置素材服务端都有”。示例箱标的二维码在图标画出来后仍能解码。
  - 已知问题：内置演示素材 logo.svg 里的文字用了 Arial，Svg.Skia 画 SVG 文字用系统字体，Linux 容器里没有这个字体时文字会缺失。正式的素材应该把文字转成路径；管理后台（T-014）上传 SVG 时可以提示。图标着色是单色（把不透明部分整体换成指定颜色），多色 SVG 图标会变成单色。
  - 留给下一位：T-014 上传素材时把文件写到数据目录的 `assets/`，文件名用素材 ID；上传后要重新加载素材库（现在只在启动时加载），可以给 `AssetLibrary` 加一个“替换整个库”的入口。上传 SVG 时先用 `SvgSanitizer` 校验。
  - 基准：改动前 203dpi 6.14 ms、300dpi 12.49 ms；改动后 5.96 ms、12.93 ms，在误差范围内。已写进 docs/performance.md。
- 2026-10-09 · Claude · 认领。计划：素材库启动时加载图标和图片；SVG 先清洗（禁 DTD、去掉脚本、事件属性、外部引用）再交给 Svg.Skia——实测 Svg.Skia 会去请求外部图片地址；图标和设计器用同一套 GB/T 191 路径；素材按目标尺寸栅格化后缓存；mono 用 Floyd–Steinberg 抖动。
