# T-026 系统信息设置和公司 Logo

状态：待验收
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
- 做的过程中补充（见交接记录）：`frontend/src/style.css`（分段按钮组，用户顺带提的问题）、`frontend/src/components/uiIcons.ts`（加齿轮图标）、`backend/tests/LabelService.Backend.Checks/AdminAccountChecks.cs`（管理员权限点数 11 → 12）、`docs/reviews/phase1-review.md`（第 7 节加第 9 条）

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `docs/tasks/README.md`：任务索引追加一行
- `backend/src/LabelService.Application/ApplicationServiceCollectionExtensions.cs`：登记新的应用服务
- `backend/src/LabelService.Infrastructure/InfrastructureServiceCollectionExtensions.cs`：登记新的仓储和 Logo 文件存放
- `AGENTS.md`：第 1 节“当前阶段”补一句

## 验收标准

- [x] 新页面“系统信息”（顶栏“系统管理”菜单里，权限点 `sys.settings`）：改系统名称、副标题、所属公司、版权信息、技术支持，上传或恢复默认公司 Logo，保存前能看到顶栏效果
- [x] 顶栏左上角显示公司 Logo（没上传时显示占位），系统名称和副标题显示在 Logo 旁边作为系统标题
- [x] 登录页、浏览器标签页标题、用户菜单显示这些设置；登录前就能读到（登录页要用）
- [x] Logo 只收 PNG、JPG、SVG，不超过 1 MB，SVG 清洗；没有 `sys.settings` 的用户不能改
- [x] 后端检查覆盖：未登录可读、无权限不能改、长度校验、上传和恢复 Logo、操作日志；三种数据库的迁移和模型一致
- [x] `verify.ps1` 通过（见 AGENTS.md）
- [x] 相关文档已更新

## 不做

- 多套主题配色、自定义登录页大图和宣传文字（以后有需要再加）
- 演示程序（调用端）不读这些设置

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->

### 2026-10-11 · Claude

**做了什么**：

- 后端：表 `system_setting`（键值对，以后加设置项不用改表），三种数据库的迁移 `AddSystemSettings`；Logo 文件放数据目录的 `branding/`，文件名带内容摘要（换图后地址变，浏览器不用旧缓存）。接口 `GET /api/admin/system-info`、`GET /system-info/logo` 不用登录（登录页要显示），`PUT /system-info`、`POST`/`DELETE /system-info/logo` 要新权限点 `sys.settings`（系统管理员自动有，其他角色要在“角色权限”里勾选）。图片检查（类型、1 MB、SVG 清洗）从素材上传里抽成共用的 `UploadedImages`，行为不变。修改、上传、恢复都记操作日志（类型“系统信息”）。
- 前端：顶栏左上角改成白底框里的公司 Logo（没上传时显示“公司 LOGO”占位），系统名称放在 Logo 右边作为系统标题，副标题在后面；放不下时先省略副标题，再省略系统名称，业务切换按钮不压缩。顶栏“权限管理”菜单改名“系统管理”，加“系统信息”页（`pages/SettingsPage.vue`）：五项文字带字数、Logo 上传和恢复占位、保存前的效果预览。登录页左上角、登录框下方的版权信息和技术支持、“忘记密码”提示、浏览器标签页标题、用户菜单底部都用这些设置。
- 用户顺带提的：属性面板等处的分段按钮组（单选）被拉宽时，最后一个选项后面留一段空白、看起来“最后一个选项拉太长”。改成多出的宽度平均分给各个选项（条码对齐、线条方向、纠错级别、接口调试的输出格式、新建模板的方向、预览 DPI 都量过，尾部只剩 1px 边框），不拉宽的按钮组宽度不变。

**验证**：后端检查 460 项通过（新增 9 项系统信息检查，三种数据库的迁移和模型一致）；前端检查 71 项、类型检查通过；浏览器里实测保存、上传 PNG Logo、顶栏立即更新；把系统名称和副标题设成很长的值，21 个页面在 1440、1280、1101、768 宽下都没有横向滚动、遮挡和没有提示的截断，测完已恢复。`verify.ps1` 全量通过。

**留给用户**：

- 用户说“临时给一个 Logo”，但消息里只有顶栏位置的截图、没有 Logo 文件，所以现在是“公司 LOGO”占位；预览数据目录里为了演示上传了一张自己画的“SAMPLE”测试图，并填了示例公司、版权信息和技术支持，都可以在系统信息页改掉或恢复占位。拿到正式 Logo 后在系统信息页上传即可，不用改代码。
- 读取系统信息和 Logo 不用登录（登录页要显示），公司名称、版权信息、技术支持对未登录的人可见；不要在这里填内部敏感信息。
- 顶栏菜单“权限管理”改名为“系统管理”，和原型不一致（原型没有系统信息页），需要用户认可，记在 review 第 7 节。
