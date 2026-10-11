# 导航与画布缩放浏览器回归

`layout-behavior.mjs` 在本机安装的 Chrome 中直接运行真实 Vue 导航组件、Konva 模板/拼版视口和页面；`layout-browser-harness.ts` 只从开发测试入口加载，不进入生产包。无需添加 npm 依赖，复用测试环境的 Playwright。

显式提供隔离实例及合成数据凭据，禁止指向用户正在使用的服务：

```powershell
$env:PLAYWRIGHT_MODULE = '测试环境内 Playwright 的绝对目录'
$env:LABEL_UI_URL = 'http://127.0.0.1:5183'
$env:LABEL_UI_PASSWORD = '本次合成测试账号密码'
$env:LABEL_UI_TEMPLATE = 'BOX_LABEL'
$env:LABEL_UI_EVIDENCE = '本次独立证据目录'
node frontend/tests/layout-behavior.mjs
```

覆盖 1440px 自动折叠边界、窄屏手动展开、页签切回、监听卸载；两种视口适应→resize、手动缩放/平移→resize、再次适应和模板 requestFit；真实页面 1100px 窄屏提示与 1101px 可操作区、隐藏工具栏焦点和横向溢出；拼版组件重复挂载和卸载。

另用既有浏览器边界脚本复验长内容、菜单、滚动表格和工作区可达性。原生 Chrome 125%/150% 缩放通过独立浏览器配置目录的外观设置验证并记录 DPR/视口；本脚本设置普通视口，不代替原生缩放或 Windows DPI 验收。
