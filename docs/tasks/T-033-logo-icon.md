# T-033 Logo 图标（顶栏、浏览器标签页）和 WebP 格式

状态：待验收
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

- [x] 系统信息页可以分别上传、恢复“完整 Logo”和“Logo 图标”，保存前能看到顶栏和登录页的效果
- [x] 顶栏和浏览器标签页图标用 Logo 图标，没有时用完整 Logo；登录页用完整 Logo（更大），没有时用图标
- [x] Logo、图标接受 PNG、JPG、SVG、WebP（素材仍然只收 PNG、JPG、SVG）
- [x] 后端检查覆盖图标的上传、读取、恢复，WebP Logo
- [x] 把用户给的两张 Logo 配置到预览环境（通过系统信息上传，不写进代码）
- [x] `verify.ps1` 通过（见 AGENTS.md）
- [x] 相关文档已更新

## 不做

- 服务端自动裁掉图片四周的空白（页面提示上传前裁好）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

### 2026-10-12 · Claude

**编号**：认领时用了 T-029，后来发现 Codex 的“分段按钮页面补丁独立审阅”已经是 T-029（T-030～T-032 也已被占用），按“当前最大值加 1”改成 T-033（提交 77a6a5d）；认领、索引两个提交的说明里还写着 T-029。

**做了什么**：

- 后端：系统信息加第二张图“Logo 图标”（设置项 `company.icon`，文件同样放数据目录 `branding/`，文件名带内容摘要），接口 `GET/POST/DELETE /api/admin/system-info/icon`，读取不用登录、修改要 `sys.settings`；应用服务里完整 Logo 和图标共用一套上传、读取、恢复的代码。Logo 和图标接受 WebP（按 RIFF/WEBP 文件头判断），素材仍然只收 PNG、JPG、SVG（素材要进渲染）。
- 前端：`BrandMark` 顶栏（紧凑）优先用图标、登录页（`full`）优先用完整 Logo 并显示得更大，没有时互相代替，都没有时显示占位；浏览器标签页小图标跟着图标（没有时用完整 Logo）；系统信息页的“公司 Logo”卡片分成“完整 Logo”“Logo 图标”两栏，各自上传、删除，效果预览里加了登录页的完整 Logo。
- 配置：用户给的两张 WebP（完整 Logo 2000×667、图标 1254×1254）用 Windows 自带的解码转成 PNG、裁掉四周空白（各留 4% 边距，得到 1590×455 和 1035×968），通过系统信息接口上传到预览环境（没有写进代码，原图没动）；预览数据里原来我填的示例公司名和版权信息改成了“智云科技”“© 2026 智云科技 版权所有”。

**验证**：后端检查新增 5 项（没上传时 404、无权限不能上传、上传后未登录可取且和完整 Logo 互不影响、完整 Logo 接受 WebP、素材不收 WebP、恢复图标后完整 Logo 还在）；前端检查、类型检查通过；无头 Edge 截图看了登录页（完整 Logo）、顶栏（图标）、系统信息页两栏，浏览器标签页小图标指向图标地址。`verify.ps1` 全量通过（后端 498、调用端 47、前端 84）。

**留给用户**：图标在顶栏里只有 28 像素高，用纯图形的版本最清楚；以后换 Logo 直接在系统信息页上传，建议先裁掉四周空白。
