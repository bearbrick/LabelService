# 前端（frontend）

网页设计器和管理后台：Vue 3 + TypeScript + Vite，画布直接用 Konva（不经过 vue-konva），条码预览用 bwip-js。只通过 HTTP 和模板 JSON 跟后端交互。

当前完成的是**设计器的绘制交互**（T-013）：7 种元素拖入画布，选择、移动、缩放、旋转，网格吸附和对齐参考线，对齐与分布，撤销重做，层级、锁定、隐藏，直接编辑文本（输入 `{{` 补全字段），复制粘贴，缩放平移，mm 标尺，样例值和占位符切换。模板库、登录、保存到服务端在 T-014；在那之前模板用 JSON 文件导入导出，草稿自动保存在本机浏览器。

```
frontend/
├─ package.json  package-lock.json   依赖（版本写死，不加 ^）
├─ vite.config.ts                    开发时把 /api 转给 http://localhost:5080
├─ tsconfig.json  index.html
├─ tests/checks.ts                   检查程序：用 Node 直接运行，检查不依赖浏览器的模块
└─ src/
   ├─ main.ts  App.vue  style.css    入口、顶栏（新建、打开、导出）、样式（沿用 DB Studio）
   ├─ model/                         纯数据和规则（Node 能直接运行，不碰 DOM）
   │  ├─ template.ts                 模板 JSON 的 TypeScript 镜像（契约）
   │  ├─ units.ts                    mm、pt、打印点换算
   │  ├─ elements.ts                 新建元素、补全默认值、复制
   │  ├─ bindings.ts                 {{key:format}} 预览替换和 .NET 格式子集
   │  ├─ validate.ts                 模板检查（字段 key、重复 ID、未定义的绑定）
   │  └─ fonts.ts  icons.ts  presets.ts
   ├─ editor/                        画布引擎
   │  ├─ stage.ts                    Konva 舞台：节点同步、选择框、拖动吸附、缩放平移
   │  ├─ draw.ts                     按元素类型绘制（和服务端绘制器规则一致）
   │  ├─ rulers.ts                   mm 标尺
   │  ├─ barcode.ts                  bwip-js 编码和缓存
   │  ├─ images.ts                   图片加载缓存、黑白化
   │  └─ history.ts  snapping.ts  arrange.ts  geometry.ts  clipboard.ts   纯逻辑，有检查
   ├─ stores/                        状态和全部编辑操作
   │  ├─ designer.ts                 模板、选择、撤销、草稿保存、元素和字段操作
   │  ├─ assets.ts                   图片素材（暂存在浏览器）
   │  └─ drag.ts                     从面板拖到画布的内容
   ├─ components/                    界面：工具栏、左侧面板、画布、属性面板、状态栏
   └─ samples/box-label.json         示例模板（和 backend/samples/templates 保持一致）
```

## 常用命令

```powershell
cd frontend
npm ci            # 安装依赖
npm run dev       # 开发，http://localhost:5173
npm run check     # 检查程序（Node 直接运行 tests/checks.ts）
npm run build     # 类型检查（vue-tsc）+ 打包
```

需要 Node 22.18+ 或 24（检查程序靠 Node 自带的类型擦除直接运行 .ts）；只打包和开发的话 Node 20.19+ 也可以。

开发环境下浏览器控制台里有 `window.__labelDesigner`（就是 `stores/designer.ts` 导出的全部内容），可以直接查看状态或调用操作，方便调试和自动化测试；打包后不存在。

## 要点

- `src/model/template.ts` 是后端 Contracts 的镜像，改动属于契约变更，见根目录 AGENTS.md。后端检查程序会核对它包含全部元素类型。
- `src/samples/box-label.json` 是后端示例模板的副本，后端检查程序会核对两份一致。
- `model/` 和 `editor/` 里的纯逻辑模块要能被 Node 直接运行：相对导入写 `.ts` 扩展名，只用可擦除的 TypeScript 语法（`erasableSyntaxOnly` 已开启），不碰 DOM。新增这类模块时在 `tests/checks.ts` 里补检查。
- 长度一律 mm，换算只用 `src/model/units.ts`；画布状态就是模板 JSON，所有修改通过 `stores/designer.ts` 的操作函数，改完调 `commit()` 记录撤销。
- 画布每个元素记“内容签名”，只有内容变了才重画；100 个元素时拖动一帧约 3.5 ms（目标 20 ms 以内）。
- 字体：浏览器用本机的思源黑体、思源宋体（或同款 Noto 字体）显示，服务端字体下载（T-016）完成后改为加载服务端同一份字体文件。
- 不引用 `frontend/` 以外的文件（后端检查程序会拦）。
