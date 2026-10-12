# T-033 Logo 图标（顶栏、浏览器标签页）和 WebP 格式

状态：进行中
负责人：Claude 2026-10-12
里程碑：一期验收
依赖：T-028、T-030
设计书：§arch（管理后台）

## 目标

用户给了两张 Logo（带“智云科技 ZHIYUN TECHNOLOGY”文字的完整 Logo 和只有图形的图标）。完整 Logo 在顶栏 28 像素高时文字看不清，所以系统信息里再加一个“Logo 图标”：顶栏和浏览器标签页用图标，登录页用完整 Logo（显示得更大）；没传图标时都用完整 Logo。两种都在系统信息页上传（配置，不写进代码），并接受 WebP 格式。

## 范围

可以新建、修改的文件：

- 后端：`backend/src/LabelService.Domain/SystemSettings/`、`backend/src/LabelService.Application/SystemSettings/`、`backend/src/LabelService.Application/Resources/UploadedImages.cs`（可选接受 WebP）、`backend/src/LabelService.Infrastructure/Storage/BrandingFileStore.cs`、`backend/src/LabelService.Api/Endpoints/Admin/SystemInfoEndpoints.cs`、检查程序
- 前端：`frontend/src/api/system.ts`、`frontend/src/stores/system.ts`、`frontend/src/components/admin/BrandMark.vue`、`frontend/src/components/admin/TopBar.vue`、`frontend/src/pages/SettingsPage.vue`、`frontend/src/pages/LoginPage.vue`、`frontend/src/system.css`
- 文档：`docs/admin-api.md`、`frontend/README.md`

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行

## 验收标准

- [ ] 系统信息页可以分别上传、恢复“完整 Logo”和“Logo 图标”，保存前能看到顶栏和登录页的效果
- [ ] 顶栏和浏览器标签页图标用 Logo 图标，没有时用完整 Logo；登录页用完整 Logo（更大），没有时用图标
- [ ] Logo、图标接受 PNG、JPG、SVG、WebP（素材仍然只收 PNG、JPG、SVG）
- [ ] 后端检查覆盖图标的上传、读取、恢复，WebP Logo
- [ ] 把用户给的两张 Logo 配置到预览环境（通过系统信息上传，不写进代码）
- [ ] `verify.ps1` 通过（见 AGENTS.md）
- [ ] 相关文档已更新

## 不做

- 服务端自动裁掉图片四周的空白（页面提示上传前裁好）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
