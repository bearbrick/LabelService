# T-014 管理 API 与管理后台

状态：进行中
负责人：Claude 2026-10-09
里程碑：M3
依赖：T-008（模板存储）、T-009（调用方与日志）
设计书：§3、§6、§8 鉴权；原型 docs/prototypes/label-designer-prototype.html

## 目标

按原型做出管理后台：登录、按业务分组的模板库、画布预设、数据源定义、版本发布与回滚、素材、调用方与密钥、调用日志、用户与权限。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Server/Admin/`：管理 API（REST，只给设计器用，不对外公开），内置账号 + JWT
- `frontend/src/`：路由、页面、API 客户端
- 量大，建议认领后先在交接记录里拆成子任务（T-014a、T-014b…）再分头做

## 共享文件

- `backend/src/LabelService.Server/Program.cs`
- `backend/Directory.Packages.props`：JwtBearer
- `backend/src/LabelService.Server/Endpoints/PublicApiEndpoints.cs`：调用方按业务分组授权、IP 白名单
- `backend/src/LabelService.Rendering/Assets/`：素材库支持重新加载，`SvgSanitizer` 改为 public
- `AGENTS.md`：第 1 节、R11、第 9 节
- `frontend/package.json`（如需 vue-router 等）

## 验收标准

- [ ] 原型里的页面和权限规则都能用；按钮禁用之外，服务端每个管理接口都校验权限
- [ ] 数据源定义页：保存时扫描 `{{key}}`，未定义的提示一键添加，未引用的给出提示；自动生成调用 JSON 样例
- [ ] 发布前试渲染并计时（见 T-008）
- [ ] 管理接口和开放接口的鉴权互不相通（API Key 不能调管理接口，登录态不能调开放接口）
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 对接公司统一登录（后续）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · T-014a、T-014b（后端）完成并合并；T-014c（前端页面）接着做，状态保持进行中。
  - 做了什么：`Server/Admin/`：JWT 签发和校验（`AdminTokens`、官方 JwtBearer）、`AdminFilter`（每次请求从库里读用户和角色，停用立即生效；必须先改密码时只放行个人设置）、权限点和内置角色（和原型一致）、PBKDF2 密码哈希、登录锁定、操作日志（op_log）；接口：账号、用户、角色、业务分组、模板（新建、草稿、发布带说明、版本、回滚、换分组、软删除、发布前检查）、画布预设、素材上传（按内容判断类型、SVG 清洗、上传后素材库立即重新加载）、字体、调用方（业务分组授权、IP 白名单、重置 Key）、调用日志和操作日志查询、概览、接口调试。清单写在 `docs/admin-api.md`。
  - 数据库：主库加 `business`、`app_user`、`app_role`，模板加 `business_id`、`updated_by`，版本加 `note`，预设加 `kind`，素材加 `key`，调用方的“允许的模板”改为业务分组 + IP 白名单 + AppId、说明；日志库加 `op_log`，调用日志加 `trace_id`、`ip`、`error_message`。迁移 `AddAdmin`、`AddOpLog`。
  - 开放接口：调用方按“模板所属业务分组”授权（模板换分组立即生效，缓存里的已发布模板带分组 ID），来源 IP 不在白名单返回 401。
  - 检查：新增 `AdminAccountChecks.cs`（21 项）、`AdminResourceChecks.cs`（39 项），覆盖验收标准里的权限规则、两种鉴权互不相通、发布前试渲染、调用方授权和 IP 白名单；`ArchitectureChecks` 的 500 行规则不再算工具生成的迁移文件。
  - 依赖：新增 `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12（ADR 0006）。
  - 留给前端（T-014c）：按 `docs/admin-api.md` 接；设计器的本机草稿换成服务端草稿（打开、自动保存、发布、版本、回滚），素材改用 `/assets`。
- 2026-10-09 · Claude · 认领，拆成三步：T-014a 账号与权限（登录、JWT、用户、角色、业务分组、操作日志）；T-014b 管理接口（模板、版本、画布预设、素材、调用方、调用日志、接口调试、概览），每个接口校验权限点和业务范围；T-014c 前端后台页面（按原型）和设计器接服务端（打开、保存草稿、发布、版本、回滚）。登录和权限的细节见 ADR 0006。后端 a、b 一个分支做完先合并，前端 c 再一个分支。
