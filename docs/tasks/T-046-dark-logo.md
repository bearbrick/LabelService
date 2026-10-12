# T-046 深色背景 Logo：登录页深蓝底上直接放反白 Logo

状态：待验收
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-044
设计书：§arch（管理后台）

## 目标

用户反馈：登录页左右两栏本来很协调，左上角加了白底卡片的 Logo 就不协调了，问“扣成白色透明的会不会更好”。会：深蓝底上直接放白色透明的反白 Logo，和右边白色登录区一深一浅。Logo 是客户自己配置的，不能在代码里把 Logo 变白，所以系统信息加第三张图“深色背景 Logo”（白色或浅色、透明背景）：上传了就在登录页深蓝底上直接显示，没上传还是白底卡片放完整 Logo。并按用户的 Logo 做了一张白色透明版传到测试环境。

## 范围

可以新建、修改的文件：

- 后端：`Domain/SystemSettings/SystemInfoRules.cs`、`Application/SystemSettings/`（服务、接口、Dto）、`Api/Endpoints/Admin/SystemInfoEndpoints.cs`、检查程序 `SystemInfoChecks.cs`
- 前端：`src/api/system.ts`、`src/pages/SettingsPage.vue`、`src/components/admin/LoginBrand.vue`、`src/system.css`
- 文档：`docs/admin-api.md`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行

## 验收标准

- [x] `GET / POST / DELETE /api/admin/system-info/logo-dark`：读取不用登录，上传、删除要 sys.settings；系统信息多一个 `logoDarkVersion`；和完整 Logo、图标互不影响
- [x] 系统信息页第三个上传位“深色背景 Logo”，在深蓝底上预览
- [x] 登录页：有深色背景 Logo 时直接放在深蓝底上（和白底卡片一样高，左右两栏照样对齐），没有时用白底卡片
- [x] 测试环境传了用户 Logo 的白色透明版，截图确认
- [x] `verify.ps1` 通过

## 不做

- 顶栏也用反白图标（顶栏同样是深蓝底，用户没提；要的话再加“深色背景图标”）
- 服务端自动把 Logo 变白（客户的 Logo 往往不是透明背景，自动处理效果没把握）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

- 2026-10-12 · Claude · 完成，待验收。后端照完整 Logo、图标的写法加了第三张图（设置项 `company.logoDark`，文件名前缀 `company-logo-dark-`），检查 3 项；前端 `IMAGE_API` 把三张图的地址、上传、删除放在一张表里。白色透明版：用当前 Logo 按“离白色多远”算不透明度（放大 1.6 倍让实心部分不透明），颜色统一换成白色，存在 `D:\Work\test\example\logo\zhiyun-logo-white.png`（1004×254），通过系统信息接口传到测试环境（版本 dd7286aadca2），完整 Logo 没动。无头 Edge 量登录页三行左右照样完全对齐，截图检查了登录页、系统信息页的上传位和预览。共享文件：任务索引追加一行。
