# 前端测试

## 默认离线检查

在仓库根目录执行 `npm --prefix frontend run check`，或进入 frontend 执行 `npm run check`。`checks.ts` 与相关模块覆盖模板、字段来源、绑定、历史、几何、拼版、条码、字体及过期异步请求，并核对 JSDoc 和文件规模。

`npm run build` 负责类型检查和生产构建。完整交付另跑根目录 `verify.ps1`，见 [统一测试入口](../../docs/testing.md)。

## 可选浏览器回归

三个 `.mjs` 脚本保留，分别检查实际页面行为，不属于离线 Node 检查。只使用自己新建的隔离前后端、合成数据和测试账号；**不要指向用户正在使用的服务**。测试可创建合成模板，数据保留用于复核。

测试环境需已有 Chrome 和 Playwright。仓库不新增该依赖，不自动下载浏览器；提供 `PLAYWRIGHT_MODULE`，复用现有安装。所有命令从仓库根目录运行：

这些脚本不负责创建隔离服务、改初始密码或准备夹具。先用独立数据目录部署测试后端、完成隔离账号初始化，再准备合成数据。隔离前端的 `/api`、`/fonts` 代理必须指向该测试后端；默认 `vite.config.ts` 指向 5080，只更换网页端口不能隔离用户数据。

```powershell
$env:PLAYWRIGHT_MODULE = '<已安装 Playwright 的模块路径>'
$env:LABEL_UI_URL = 'http://127.0.0.1:5183'
$env:LABEL_UI_ACCOUNT = '<隔离测试账号>'
$env:LABEL_UI_PASSWORD = '<本次隔离账号密码>'
$env:LABEL_UI_EVIDENCE = '<本次独立截图与结果目录>'
```

### 表格和工作区边界

```powershell
$env:LABEL_UI_FIXTURE = '<合成夹具 JSON 文件路径>'
npm --prefix frontend run check:browser:boundaries
```

先准备长 AppId、负责人、角色及模板等合成数据。夹具字段为 `appId`、`name`、`account`、`code`，账号要有对应页面权限；脚本确认真实单元格存在，避免空数据误报。

覆盖业务、调用方、用户、角色、操作日志、预设和模板设计页面的 35 个页面/尺寸组合，尺寸包括 1366×768、1920×1080、1280×720、1366×600、1024×600；检查相邻列遮挡、页面溢出、工作区可达性及浏览器异常，保存截图和 `results.json`。

### 导航与画布行为

```powershell
$env:LABEL_UI_TEMPLATE = 'BOX_LABEL'
npm --prefix frontend run check:browser:layout
```

`layout-browser-harness.ts` 只由开发测试入口加载，不进入生产包。覆盖真实 Vue 导航、Konva 模板与拼版视口：1440px 自动折叠边界、窄屏手动展开、切回页签、监听卸载、适应窗口/手动缩放/平移后的 resize，以及 1100/1101px 可操作边界。

### 分段按钮

```powershell
$env:LABEL_UI_BACKEND = 'http://127.0.0.1:5189'
npm --prefix frontend run check:browser:segmented
```

默认 `after` 模式，检查分段组末尾空白、按钮高度、实际点选状态、溢出和异常；覆盖元素属性、API 格式、模板筛选、新建方向与服务端预览 DPI。

直接运行 `node frontend/tests/segmented-browser.mjs before` 仅用于记录历史原布局的空白，不算修复后通过。不要把此模式或历史截图当作当前回归结果。

## 品牌组件

开发前端打开 `/tests/brand-mark.html`，直接渲染实际 `BrandMark.vue` 和产品样式，检查深色 Logo、普通 Logo、仅图标、无图片四种参数。页面只读公开系统信息，不上传或保存配置；状态显示 5 项 DOM 检查结果。

仍应在实际登录页面核对导航、不同窗口宽度和系统信息预览；测试页只是组件展示检查。该 HTML 位于 tests，不进入 Vite 的产品入口和发布包。

## 证据边界

每次在独立输出目录保存截图和 DOM/断言结果，记录源码版本、浏览器、窗口尺寸与未覆盖项。旧文档中仓库外的 `../evidence/phase10` 等路径是当时工作区的历史产物，不是当前测试前置条件。

原生 Chrome 125%/150% 缩放要用独立配置的外观设置复核；本脚本的 viewport 不代替原生缩放或操作系统 DPI。已提交历史证据见 [评审入口](../../docs/reviews/README.md)。
