# 分段按钮实际浏览器回归

`segmented-browser.mjs` 使用本机已安装的 Chrome 和测试环境已有 Playwright，在显式指定的本地隔离服务中创建合成模板。测试数据保留，不对用户运行的服务执行。

```powershell
$env:PLAYWRIGHT_MODULE = '测试环境现有 Playwright 的绝对路径'
$env:LABEL_UI_URL = 'http://127.0.0.1:5183'
$env:LABEL_UI_BACKEND = 'http://127.0.0.1:5189'
$env:LABEL_UI_PASSWORD = '本次合成管理员密码'
$env:LABEL_UI_EVIDENCE = '本次独立证据目录'
node frontend/tests/segmented-browser.mjs after
```

`before` 模式仅记录原布局空白，不把预期存在的空白当作测试执行错误。`after` 检查组末尾空白不超过 1.5 CSS 像素、按钮高度、页面溢出、真实点选状态及浏览器异常。覆盖七种元素属性、API 输出格式、模板筛选/显示方式、新模板方向和服务端预览 DPI。

修复后覆盖 1920×1080、1280×720、1101×700，以及独立 Chrome 配置中的原生 125%/150%（物理窗口 1920×1080）。测试不改变 OS DPI。紧凑组修复前后总宽比较另存于证据 `comparison.json`。
