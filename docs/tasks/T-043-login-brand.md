# T-043 登录页品牌区：Logo 单独一行，系统名称做标题

状态：待验收
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-033
设计书：§arch（管理后台）

## 目标

用户反馈：登录页左上角“Logo 白底框 | 标签服务”放在一行总觉得不协调。原因：Logo 是一大块亮白的框，旁边的系统名称只有 18px，两边轻重差太多，中间的竖线也多余；下面的公司名称“智云科技”又和 Logo 里的字重复。改成：Logo 单独一行（白底圆角卡片，稍小一些、带一点阴影），下面是系统名称（作为标题，26px）和副标题；上传了 Logo 时不再重复写公司名称，没有 Logo 时用公司名称代替。系统信息页的“效果预览”同步。

## 范围

可以新建、修改的文件：

- `frontend/src/components/admin/LoginBrand.vue`（新建）、`BrandMark.vue`（去掉只给登录页用的 full）
- `frontend/src/pages/LoginPage.vue`、`frontend/src/pages/SettingsPage.vue`、`frontend/src/system.css`、`frontend/README.md`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行

## 验收标准

- [x] 登录页：Logo 单独一行，下面系统名称和副标题，没有竖线，不重复公司名称
- [x] 系统信息页的登录页预览和登录页一致
- [x] 1440×900、1366×768 截图检查
- [x] `verify.ps1 -Part frontend` 通过

## 不做

- 顶栏的 Logo（用的是 Logo 图标，用户没提）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 完成，待验收。新建 `LoginBrand`（Logo 先用完整 Logo、没有时用图标；都没有时显示公司名称），登录页和系统信息页预览共用（预览用 compact 缩小）；`BrandMark` 只剩顶栏用法，去掉 full 和对应样式。无头 Edge 截图检查了 1440×900、1366×768 的登录页和系统信息页的预览。共享文件：任务索引追加一行。
