# 表格与工作区边界浏览器回归

模板设计页还会通过实际滚轮滚动工作区，核对标签页及设计区完整进入视窗；有效宽度低于既有 1100 CSS px 门槛时，核对窄窗口提示的两行文字完整可达。短高度和超长标题不能把内容压到零高度。

原生缩放通过此电脑 Chrome 的 `chrome://settings/appearance` 设置 125%/150%，同时记录物理截图尺寸、CSS 视窗宽度和 `visualViewport.scale`；不传入 `deviceScaleFactor` 或使用 CSS zoom。操作系统显示缩放仍未测。最新整合的独立证据位于仓库外 `../evidence/phase10`，历史截图不作为新版本通过证据。

`browser-boundaries.mjs` 在真实 Chrome 中检查 1366×768、1920×1080、1280×720、1366×600、1024×600 下的业务、调用方、用户、角色、操作日志、预设和模板设计页面，共 35 个组合。检查单元格内容宽度及文档是否被撑宽，保留截图和 `results.json`；浏览器异常或内容越过相邻列会失败。它是独立的浏览器测试，常规 `npm run check` 不包含它。

仅使用自建隔离服务及合成数据。先在该服务中准备 64 字符 AppId、64 字符负责人姓名、长角色名称、负责人业务关联及该用户的登录日志。将以下字段写入测试夹具 JSON：`appId`、`name`、`account`、`code`（长名称模板编码）。测试会确认真实 AppId、用户账号和负责人单元格存在，避免空夹具误报通过。管理员必须具有页面访问权限。

使用已安装的 Playwright 和 Chrome；本测试不要求新增生产依赖。设置环境变量后运行：

```powershell
$env:PLAYWRIGHT_MODULE = '<已安装 Playwright 模块路径>'
$env:LABEL_UI_URL = 'http://127.0.0.1:<隔离前端端口>'
$env:LABEL_UI_ACCOUNT = '<隔离管理员账号>'
$env:LABEL_UI_PASSWORD = '<隔离测试密码>'
$env:LABEL_UI_FIXTURE = '<合成夹具 JSON 路径>'
$env:LABEL_UI_EVIDENCE = '<本次截图输出目录>'
node tests/browser-boundaries.mjs
```

回归覆盖 35 个页面尺寸组合；此外重新检查完整页面矩阵、合成长用户与角色、菜单、弹窗及原生缩放。具体通过项数以当前候选的证据报告为准，原始截图需与 DOM 结果一起复核。
