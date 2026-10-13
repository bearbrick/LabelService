# T-053 顶栏使用透明的完整品牌 Logo

状态：待验收
负责人：Codex 2026-10-13
里程碑：一期验收
依赖：T-046 深色背景 Logo 已实现
设计书：§arch、§designer（沿用已有管理外壳）

## 目标

按用户确认，顶部品牌区优先使用已经上传的白色透明长 Logo，取消该图的白底卡片；保留系统名称与返回工作台入口，让用户在实际页面试效果。

## 范围

- frontend/src/components/admin/BrandMark.vue：优先深色背景 Logo，兼容完整 Logo、图标和占位。
- frontend/src/system.css：透明 Logo 的大小、宽度及窄窗口布局。
- frontend/src/pages/SettingsPage.vue：同步三类品牌图片用途说明和顶栏预览。
- frontend/tests/brand-mark.html：直接加载实际品牌组件和样式，检查四种图片配置的展示回退，不修改系统配置。
- frontend/README.md、本任务文件及任务索引：实现和验证记录。

## 共享文件

- docs/tasks/README.md：追加任务索引，单独提交。

## 验收标准

- [x] 深色背景长 Logo 在顶栏直接透明显示，按比例缩放，系统名称和入口保留。
- [x] 没有深色 Logo 时仍能显示完整 Logo、图标或占位，系统信息页预览与顶栏一致。
- [x] 实际宽窄窗口检查，无新增导航重叠；保存截图并恢复浏览器视口。
- [x] verify.ps1 通过，文档和任务交接记录同步。

## 不做

- 不生成或重绘 Logo，不改已上传的图片及系统配置，不新增上传槽或依赖。
- 不改客户端图标、浏览器 favicon、登录布局、契约、数据库及渲染链路。

## 交接记录

- 2026-10-13 · Codex · BrandMark 优先显示深色背景完整 Logo，取消该图白底卡片；未上传时按完整 Logo、图标、占位回退。沿用现有 URL 和上传版本，不加工或改系统图片，不改 favicon、登录页和客户端。深色长图宽屏最高 36px，较窄窗口最高 30px；901～1180px 收紧顶栏间距，并省略系统名称以优先保留完整公司 Logo 和导航文字，系统名称在宽屏、一般小窗口仍按原规则展示。普通 Logo 在该临界段也限宽。系统信息页共用品牌组件预览，三个上传槽的用途说明已同步。
  - 验证：verify.ps1 全量通过（后端 529、调用端 137、前端 117），日志 artifacts/header-logo/verify-final.log。后续临界宽度 CSS 修正再跑 frontend 验证，仍通过 117 项及类型检查/构建；日志 verify-frontend-final.log。最初验证因运行中的 API 锁定 DLL 失败，已关闭本会话程序重跑成功并恢复运行，未删除数据或改密码。
  - 浏览器检查：frontend/tests/brand-mark.html 直接加载真实 Vue 组件及产品样式，仅读公开系统信息，四种图片配置下 5 项 DOM 断言通过；图片实际加载正常，360px 下组件无横向溢出。用户重新登录后，核对真实 BOX_LABEL 页面在 1440、901、360px 下的顶栏。901px 时导航右缘 885.33px，在 901.33px 的顶栏内；360px 时 Logo 右缘 170.57px、右侧用户菜单左缘 278px，没有重叠。宽屏 Logo 的背景实际为透明，URL 为现有 logo-dark。未编辑/发布模板。截图 artifacts/header-logo/header-desktop.png、header-brand.png、header-901.png、header-narrow.png、brand-fallbacks.png、brand-narrow.png；临时测试页已关闭，视口已恢复。
  - 不改契约、渲染和架构，无基准要求；README 同步展示与回退规则，设计书未改。代码只在本任务分支，未推送或合并 main。待用户确认实际品牌视觉效果。

- 2026-10-13 · Codex · 用户确认“图标的那个也改了吧，试试效果”，对应前面讨论的顶部品牌区白色透明长 Logo。复用现有深色背景 Logo，不另做图像加工。共享任务索引已登记；新任务从 main 建分支，快进接续已完成的本会话改动，不合并 main、不推送。
