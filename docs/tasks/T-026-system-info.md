# T-026 系统信息设置和公司 Logo

状态：进行中
负责人：Claude 2026-10-11
里程碑：一期验收
依赖：T-025
设计书：§arch（管理后台）

## 目标

管理员可以在管理后台设置系统名称、副标题、所属公司、公司 Logo（上传）、版权信息、技术支持联系方式；顶栏左上角显示公司 Logo，系统名称作为系统标题显示在旁边；登录页、浏览器标签页标题、用户菜单跟着这些设置显示。

## 范围

可以新建、修改的文件：

- 后端：`backend/src/LabelService.Domain/SystemSettings/**`、`backend/src/LabelService.Domain/Identity/PermissionNames.cs`（新权限点 `sys.settings`）、`backend/src/LabelService.Application/SystemSettings/**`、`backend/src/LabelService.Application/Resources/`（把图片识别抽成共用的）、`backend/src/LabelService.Infrastructure/`（实体映射、仓储、Logo 文件存放、三种数据库的迁移）、`backend/src/LabelService.Api/Endpoints/Admin/`（新接口）
- 后端检查：`backend/tests/LabelService.Backend.Checks/`
- 前端：`frontend/src/api/`、`frontend/src/stores/`、`frontend/src/pages/`、`frontend/src/components/admin/`、`frontend/src/App.vue`、样式、`frontend/index.html`
- 文档：`docs/admin-api.md`、`backend/README.md`、`frontend/README.md`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行
- `backend/src/LabelService.Application/ApplicationServiceCollectionExtensions.cs`：登记新的应用服务
- `backend/src/LabelService.Infrastructure/InfrastructureServiceCollectionExtensions.cs`：登记新的仓储和 Logo 文件存放
- `AGENTS.md`：第 1 节“当前阶段”补一句

## 验收标准

- [ ] 新页面“系统信息”（顶栏“系统管理”菜单里，权限点 `sys.settings`）：改系统名称、副标题、所属公司、版权信息、技术支持，上传或恢复默认公司 Logo，保存前能看到顶栏效果
- [ ] 顶栏左上角显示公司 Logo（没上传时显示占位），系统名称和副标题显示在 Logo 旁边作为系统标题
- [ ] 登录页、浏览器标签页标题、用户菜单显示这些设置；登录前就能读到（登录页要用）
- [ ] Logo 只收 PNG、JPG、SVG，不超过 1 MB，SVG 清洗；没有 `sys.settings` 的用户不能改
- [ ] 后端检查覆盖：未登录可读、无权限不能改、长度校验、上传和恢复 Logo、操作日志；三种数据库的迁移和模型一致
- [ ] `verify.ps1` 通过（见 AGENTS.md）
- [ ] 相关文档已更新

## 不做

- 多套主题配色、自定义登录页大图和宣传文字（以后有需要再加）
- 演示程序（调用端）不读这些设置

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
