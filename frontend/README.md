# 前端（frontend）

网页设计器和管理后台：Vue 3 + TypeScript + Vite，画布直接用 Konva（不经过 vue-konva），条码预览用 bwip-js。只通过 HTTP 和模板 JSON 跟后端交互。

已完成：

- **设计器**（T-013）：7 种元素拖入画布，选择、移动、缩放、旋转，网格吸附和对齐参考线，对齐与分布，撤销重做，层级、锁定、隐藏，直接编辑文本（输入 `{{` 补全字段），复制粘贴，缩放平移，mm 标尺，样例值和占位符切换。
- **管理后台**（T-014，按原型 `docs/prototypes/label-designer-prototype.html`）：登录（首次登录必须改密码）、工作台、按业务分组的模板库（服务端缩略图）、模板工作区（设计、数据源、接口调试、版本记录）、业务分组、画布预设、素材与字体、调用方与密钥、调用日志、操作日志、用户、角色权限、个人设置。接口见 `docs/admin-api.md`。
- 模板从服务端打开，修改后 1 秒自动保存为服务端草稿；发布前服务端检查并试渲染；历史版本可以复制回草稿。没有编辑权限时设计器只读（改动会被撤回）。模板 JSON 仍可导入（替换当前内容，可撤销）、导出。

开发时先启动后端（`dotnet run --project backend/src/LabelService.Server`），首次登录用 `admin` / `admin123`，按提示改密码。

```
frontend/
├─ package.json  package-lock.json   依赖（版本写死，不加 ^）
├─ vite.config.ts                    开发时把 /api 转给 http://localhost:5080
├─ tsconfig.json  index.html
├─ tests/checks.ts                   检查程序：用 Node 直接运行，检查不依赖浏览器的模块
└─ src/
   ├─ main.ts  App.vue               入口；外壳（登录页，或顶栏 + 模板树 + 当前页面）
   ├─ style.css  admin.css           设计器样式（沿用 DB Studio）、管理后台样式（从原型移植）
   ├─ api/                           管理接口客户端
   │  ├─ http.ts                     带登录令牌的请求、统一错误（ApiError），401 时回登录页
   │  └─ admin.ts                    和 docs/admin-api.md 对应的类型和函数
   ├─ model/                         纯数据和规则（Node 能直接运行，不碰 DOM）
   │  ├─ template.ts                 模板 JSON 的 TypeScript 镜像（契约）
   │  ├─ units.ts                    mm、pt、打印点换算
   │  ├─ elements.ts                 新建元素、补全默认值、复制
   │  ├─ bindings.ts                 {{key:format}} 预览替换和 .NET 格式子集、字段改名
   │  ├─ validate.ts                 模板检查（字段 key、重复 ID、未定义的绑定）
   │  ├─ request.ts                  调用样例（和后端发布检查的样例数据规则一致）
   │  ├─ format.ts  chart.ts         时间、尺寸、文件大小的显示；工作台柱状图几何
   │  └─ fonts.ts  icons.ts
   ├─ editor/                        画布引擎
   │  ├─ stage.ts                    Konva 舞台：节点同步、选择框、拖动吸附、缩放平移
   │  ├─ draw.ts                     按元素类型绘制（和服务端绘制器规则一致）
   │  ├─ rulers.ts                   mm 标尺
   │  ├─ barcode.ts                  bwip-js 编码和缓存
   │  ├─ images.ts                   图片加载缓存、黑白化
   │  └─ history.ts  snapping.ts  arrange.ts  geometry.ts  clipboard.ts   纯逻辑，有检查
   ├─ stores/                        状态和操作
   │  ├─ designer.ts                 模板、选择、撤销、自动保存、只读、元素和字段操作
   │  ├─ workspace.ts                模板工作区：从服务端打开、保存草稿、发布、版本、回滚、换分组
   │  ├─ session.ts                  登录状态、权限点 can()、业务范围 inScope()
   │  ├─ router.ts                   hash 路由（#/templates/3、#/t/编码/fields…）
   │  ├─ catalog.ts                  共用列表：业务分组、模板、画布预设、角色
   │  ├─ dialogs.ts                  确认框、一次性密钥框、新建模板对话框、复制
   │  ├─ assets.ts                   图片素材（服务端素材库，按需下载成 blob URL）
   │  └─ drag.ts                     从面板拖到画布的内容
   ├─ pages/                         管理后台的页面（每页一个文件）
   ├─ components/                    设计器：工具栏、左侧面板、画布、属性面板、状态栏
   │  ├─ admin/                      顶栏、模板树、弹窗、新建模板、缩略图、接口调试面板
   │  └─ workspace/                  模板工作区的数据源、版本记录、发布对话框
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
- 长度一律 mm，换算只用 `src/model/units.ts`；画布状态就是模板 JSON，所有修改通过 `stores/designer.ts` 的操作函数，改完调 `commit()` 记录撤销并安排自动保存（保存函数由 `stores/workspace.ts` 注册）。
- 权限：按钮的禁用、菜单的隐藏用 `can()`、`inScope()`，只是体验；服务端每个管理接口都会再校验。页面需要的权限点登记在 `App.vue` 的 `PAGES`。
- 不引入 vue-router：路由只有十几个页面，`stores/router.ts` 用 hash 实现，刷新停在原位。
- 画布每个元素记“内容签名”，只有内容变了才重画；100 个元素时拖动一帧约 3.5 ms（目标 20 ms 以内）。
- 字体：浏览器用本机的思源黑体、思源宋体（或同款 Noto 字体）显示，服务端字体下载（T-016）完成后改为加载服务端同一份字体文件。
- 不引用 `frontend/` 以外的文件（后端检查程序会拦）。
