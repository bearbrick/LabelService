# 管理接口 /api/admin

只给设计器和管理后台用，不对外公开，可以随前端一起调整（改了要同步本文和后端检查程序）。登录和权限的设计见 [ADR 0006](adr/0006-admin-auth.md)。

## 约定

- 鉴权：`Authorization: Bearer <令牌>`。令牌由 `POST /api/admin/login` 签发，8 小时；勾选“7 天内自动登录”时 7 天。开放接口的 `X-Api-Key` 在这里无效，令牌在开放接口也无效。
- 每次请求都会从库里核对用户：停用、改角色、改业务范围立即生效。
- 错误统一是 `{ "code": "...", "message": "...", "details": [] }`：

| HTTP | code | 含义 |
| --- | --- | --- |
| 400 | `INVALID` | 输入有问题，`message` 和 `details` 写明原因（发布检查不通过时 `details` 逐条列出） |
| 401 | `UNAUTHORIZED` | 没登录、令牌无效或过期、账号已停用：跳回登录页 |
| 401 | `INVALID_LOGIN` | 账号或密码不对 |
| 403 | `LOCKED` | 连续输错 5 次，锁定 15 分钟 |
| 403 | `DISABLED` | 账号已停用 |
| 403 | `PASSWORD_CHANGE_REQUIRED` | 首次登录或被重置密码后必须先改密码，只能访问个人设置相关接口 |
| 403 | `FORBIDDEN` | 没有这个权限点，或目标业务分组不在自己的范围里 |
| 404 | `NOT_FOUND` | 不存在，或不在自己的业务范围里（不暴露是否存在） |

- 时间都是 UTC（ISO 8601），前端按本地时区显示。查询参数里的时间可以带时区。
- JSON 属性名是 camelCase。

## 权限点

| 权限点 | 说明 |
| --- | --- |
| `tpl.view` | 查看模板、概览 |
| `tpl.edit` | 新建、编辑模板（保存草稿、放弃草稿、回滚成草稿、换分组） |
| `tpl.publish` | 发布模板 |
| `tpl.delete` | 删除模板 |
| `biz.manage` | 管理业务分组 |
| `res.manage` | 管理画布预设、素材 |
| `api.test` | 接口调试 |
| `api.manage` | 管理调用方和密钥 |
| `log.view` | 查看调用日志、操作日志 |
| `sys.user` | 管理用户 |
| `sys.role` | 管理角色权限 |
| `sys.settings` | 修改系统信息（系统名称、公司 Logo、版权信息） |

模板相关的接口同时检查权限点和业务范围。调用方接口也看业务范围（T-021）：业务范围不是“全部业务”的用户，列表里只有授权完全在自己范围里的调用方，修改、启停、重置 Key 别的调用方返回 404；新建、修改时只能授权自己范围里的分组，不能授权“全部业务”，否则 403。下表“权限”一列写“登录”的，只要登录（且不需要先改密码）就能用。

## 接口

| 方法 | 路径 | 权限 | 说明 |
| --- | --- | --- | --- |
| POST | `/login` | — | `{ account, password, remember }` → `{ token, expiresAt, me }` |
| POST | `/logout` | 登录* | 记一条操作日志 |
| GET | `/me` | 登录* | 当前用户：`id、account、name、phone、role{id,name}、permissions[]、allBusinesses、businessIds[]、mustChangePassword、lastLoginAt` |
| PUT | `/me` | 登录* | `{ name, phone }` |
| POST | `/me/password` | 登录* | `{ oldPassword, newPassword }`；新密码至少 8 位，同时有字母和数字 |
| GET | `/permissions` | 登录* | 权限点目录 `[{ key, label, group }]` |
| GET | `/users` | sys.user | 用户列表 |
| POST | `/users` | sys.user | `{ account, name, roleId, allBusinesses, businessIds[] }` → `{ user, temporaryPassword }` |
| PUT | `/users/{id}` | sys.user | `{ name, roleId, allBusinesses, businessIds[] }`；不能改自己的角色 |
| POST | `/users/{id}/status` | sys.user | `{ enabled }`；不能停用自己 |
| POST | `/users/{id}/reset-password` | sys.user | → `{ temporaryPassword }`，同时解锁 |
| GET | `/roles` | 登录 | 角色列表（含 `permissions[]`、`builtin`、`users` 人数） |
| POST | `/roles` | sys.role | `{ name, description, permissions[] }` 或 `{ name, description, copyFromRoleId }` |
| PUT | `/roles/{id}` | sys.role | `{ name, description, permissions[] }`；系统管理员角色不能改 |
| DELETE | `/roles/{id}` | sys.role | 内置角色、有人在用的角色不能删 |
| GET | `/businesses` | 登录 | 自己范围里的业务分组（含 `templates` 数量） |
| POST / PUT | `/businesses`、`/businesses/{id}` | biz.manage | `{ code, name, description, ownerUserId, color }` |
| DELETE | `/businesses/{id}` | biz.manage | 分组里还有模板时不能删 |
| GET | `/templates` | tpl.view | 模板列表（自己范围里的）：`code、name、businessId、status、latestVersion、hasDraft、createdBy、updatedBy、updatedAt` |
| POST | `/templates` | tpl.edit | `{ code, name, businessId, width, height, dpi }` 建空白模板，或带 `content`（完整模板 JSON） |
| POST | `/templates/check` | tpl.view | 请求体是模板 JSON：发布前检查 + 试渲染，不保存 → `{ errors[], warnings[], renderMs, canPublish }` |
| GET | `/templates/{code}` | tpl.view | 打开：有草稿给草稿，没有给最新发布版本；`{ ..., hasDraft, content }` |
| DELETE | `/templates/{code}` | tpl.delete | 软删除：开放接口立即不能用，编码不能再用 |
| PUT | `/templates/{code}/draft` | tpl.edit | 请求体是模板 JSON（编码不能改）→ `{ draftVersion, savedAt }` |
| DELETE | `/templates/{code}/draft` | tpl.edit | 放弃未发布的改动 |
| POST | `/templates/{code}/publish` | tpl.publish | `{ note }` → `{ version, warnings[], renderMs }`；检查不通过时 400，`details` 逐条列出 |
| GET | `/templates/{code}/versions` | tpl.view | `[{ versionNo, status, publishedBy, publishedAt, note }]` |
| GET | `/templates/{code}/versions/{n}` | tpl.view | 某个已发布版本的模板 JSON |
| POST | `/templates/{code}/versions/{n}/restore` | tpl.edit | 回滚第一步：把 vN 复制成草稿 → `{ draftVersion }` |
| PUT | `/templates/{code}/business` | tpl.edit | `{ businessId }`，新旧分组都要在自己范围里 |
| GET | `/presets` | 登录 | 画布预设 `[{ id, name, kind, widthMm, heightMm, sort, enabled }]` |
| POST / PUT / DELETE | `/presets`、`/presets/{id}` | res.manage | `{ name, kind, width, height, enabled }` |
| GET | `/assets` | 登录 | `[{ key, name, builtin, size, createdAt }]`；模板里写 `asset:key` |
| GET | `/assets/{key}/file` | 登录 | 素材文件（设计器显示用） |
| POST | `/assets` | res.manage | multipart，字段 `file`；PNG、JPG、SVG，≤ 1 MB；按内容判断类型，SVG 先清洗 |
| PUT / DELETE | `/assets/{key}` | res.manage | `{ name }` 改名 / 删除（内置素材不能改、不能删） |
| GET | `/fonts` | 登录 | `{ families[], files[] }` |
| GET | `/layouts` | 登录 | 拼版版式 `[{ layout, enabled, updatedBy, updatedAt }]`，`layout` 是版式 JSON（`docs/contracts.md`“拼版”）；带 `slotWidth`、`slotHeight` 时只列格子尺寸相同的（模板详情页“可用的版式”） |
| GET | `/layouts/{code}` | 登录 | 一个版式，同上 |
| POST | `/layouts` | res.manage | `{ layout, enabled? }` 新建；长度按 0.1 mm 取整后检查（格子越界、重叠、旋转角度），不通过时 400，`details` 逐条列出（带格子序号）；编码重复 400 |
| PUT | `/layouts/{code}` | res.manage | `{ layout, enabled? }` 修改或停用（编码不能改），立即生效：停用后开放接口返回 `LAYOUT_NOT_FOUND` |
| DELETE | `/layouts/{code}` | res.manage | 删除版式 |
| POST | `/layouts/preview` | tpl.view | 预览还没保存的版式：`{ layout, templateCode, version?, format?（png / pdf，默认 png）, dpi?, startSlot?, items?[{ data, copies }] }`；不给 `items` 时用模板样例值铺满一页 → PNG（多页时 ZIP）或 PDF，带 `X-Label-Count`、`X-Page-Count` |
| POST | `/layouts/test-page` | 登录 | 打印校准测试页：`{ layout, format?（默认 pdf）, dpi? }`，每个格子画边框、中心十字和序号，按版式（含打印偏移）排一页 |
| GET | `/clients` | api.manage | 调用方列表（只有 `keyPrefix`，没有 Key） |
| POST | `/clients` | api.manage | `{ name, appId, description, allBusinesses, businessIds[], ipAllowlist }` → `{ client, apiKey }`（Key 只返回这一次） |
| PUT | `/clients/{id}` | api.manage | 同上（不改 Key） |
| POST | `/clients/{id}/status` | api.manage | `{ enabled }`，停用立即生效 |
| POST | `/clients/{id}/reset-key` | api.manage | → `{ client, apiKey }`，旧 Key 立即失效 |
| GET | `/call-logs` | log.view | 参数 `from、to、templateCode、clientId、status、q（链路 ID / X-Request-Id / 模板编码 / 错误码）、beforeTime、beforeId、pageSize`；按时间倒序，翻页用上一页最后一条的 `createdAt`、`id` |
| GET | `/op-logs` | log.view | 参数 `from、to、userId、type、q、beforeId、pageSize` |
| GET | `/dashboard` | tpl.view | `templates{total,published,changed,neverPublished}`；有 log.view 时再给 `today{total,errors,slow,labels,averageMs,p95Ms}`（p95Ms 是今日单张成功调用的 P95）、`days[14]`、`topTemplates`、`slowTemplates`（近 7 天单张平均耗时最高的 5 个）、`recentOps` |
| POST | `/api-test/render` | api.test | 请求体和开放接口单张生成一样，用已发布的模板试生成，返回文件；不写调用日志 |
| GET | `/system-info` | — | 系统信息 `{ systemName, subtitle, companyName, copyright, support, logoVersion? }`；不用登录（登录页要显示）；没上传 Logo 时没有 `logoVersion` |
| PUT | `/system-info` | sys.settings | `{ systemName, subtitle, companyName, copyright, support }`；去掉首尾空白、换行换成空格；系统名称必填，最长 20 / 30 / 60 / 120 / 60 字 |
| GET | `/system-info/logo` | — | 公司 Logo 文件，不用登录；前端在地址后面拼 `?v={logoVersion}`；没上传时 404 |
| POST | `/system-info/logo` | sys.settings | multipart，字段 `file`；PNG、JPG、SVG，≤ 1 MB；按内容判断类型，SVG 先清洗；替换原来的 Logo → 系统信息 |
| DELETE | `/system-info/logo` | sys.settings | 删除上传的 Logo，恢复成占位 → 系统信息 |
| POST | `/preview` | tpl.view | 服务端预览（ADR 0007）：`{ template, data?, dpi?, mono? }`，模板不用先保存，不传 data 时用字段样例值 → PNG（带 `X-Label-Dpi`、`Server-Timing`、`X-Label-Warnings`）；模板 JSON 不合法时 400，`message` 写 JSON 路径和行；结构问题（元素 ID 重复等）逐条列在 `details` |

\* 必须先改密码的用户也能访问。

## 字体（不在 /api/admin 下，不需要登录）

- `GET /fonts/fonts.css`：每个模板字体名（`SourceHanSansSC`、`SourceHanSerifSC`）一条 `@font-face`，家族名 `LabelService SourceHanSansSC` 等，指向服务端渲染实际用的字体文件；用 `ascent-override`、`descent-override` 把浏览器的字体度量设成服务端的值。设计器在 `index.html` 里引入。
- `GET /fonts/{文件名}`：字体文件，只提供已加载的文件，地址带版本号，缓存一年。
