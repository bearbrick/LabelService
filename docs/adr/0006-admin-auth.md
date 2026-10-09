# 0006 管理后台用内置账号 + JWT，权限按“角色 × 业务范围”控制

状态：已采纳
日期：2026-10-09
关联：T-014、T-016

## 背景

设计书第 8 节定了“设计器和管理 API 用登录态：初版内置账号 + JWT，后续对接公司统一登录”。高保真原型（`docs/prototypes/label-designer-prototype.html`）给出了权限模型：11 个权限点、可自定义的角色、每个用户只能看和改自己负责的业务分组；调用方也按业务分组授权，可以设 IP 白名单。实现时要定下几个细节：用哪个库签发和校验 JWT、密码怎么存、令牌放哪里、停用用户怎么立即生效。

## 决定

- **令牌**：登录接口 `POST /api/admin/login` 签发 JWT（HS256），有效期 8 小时，勾选“7 天内自动登录”时 7 天。校验用微软的 `Microsoft.AspNetCore.Authentication.JwtBearer`（新增依赖，ASP.NET Core 官方包），不自己写签名校验。
- **签名密钥**：配置 `LabelService:Admin:SigningKey`（Base64，至少 32 字节）；没有配置时首次启动在数据目录生成 `admin-signing.key` 并沿用（单实例部署，ADR 0003）。密钥不进仓库。
- **密码**：PBKDF2-SHA256，随机盐，迭代 210000 次，格式 `pbkdf2-sha256$迭代次数$盐$哈希`。只用 .NET 自带的 `Rfc2898DeriveBytes.Pbkdf2`，不引入 Identity。连续输错 5 次锁定 15 分钟。
- **首次启动**：库里没有用户时建一个 `admin`（系统管理员）。密码取配置 `LabelService:Admin:InitialPassword`；没配置时开发环境用 `admin123`，其他环境随机生成并写一条警告日志。首次登录后必须改密码。
- **每次请求重新核对用户**：令牌里只放用户 ID；每个管理接口请求都从库里读用户和角色（管理接口不在热路径上），所以停用用户、改角色、改业务范围立即生效，不用等令牌过期。
- **权限**：角色有一组权限点（`tpl.view`、`tpl.edit`、`tpl.publish`、`tpl.delete`、`biz.manage`、`res.manage`、`api.test`、`api.manage`、`log.view`、`sys.user`、`sys.role`），“系统管理员”角色拥有全部权限且不能修改、删除。用户另有业务范围（全部或若干业务分组）：模板相关的操作同时检查权限点和业务范围。
- **前端保存令牌**：勾选“7 天内自动登录”时放 localStorage，否则放 sessionStorage。管理接口只认 `Authorization: Bearer`，不用 Cookie，不需要 CSRF 防护。
- **和开放接口隔离**：开放接口只认 `X-Api-Key`，管理接口只认 JWT，两者互不相通（检查程序验证）。
- **调用方授权改为按业务分组**：`api_client` 存可调用的业务分组（或全部）和 IP 白名单（CIDR，逗号分隔，空表示不限）。开放接口按“模板所属业务分组”判断，模板换分组后立即生效。来源 IP 不在白名单时返回 401 `UNAUTHORIZED`，消息里写明原因。
- **操作日志**：登录、退出和所有写操作记一条操作日志（谁、什么时候、什么操作、对象、说明、IP），放在日志库 `label-log.db` 的 `op_log` 表，保留期和调用日志相同。

## 备选方案

- **Cookie 登录**：同源部署时更简单，也不用在前端存令牌；但设计书已经定了 JWT，后续对接公司统一登录（OIDC）时 JWT 也更顺。
- **自己实现 HS256 签名校验**：省一个依赖，但安全相关的代码用官方实现更稳妥。
- **令牌里放权限，不每次查库**：省一次查询，但停用、改权限要等令牌过期才生效，不符合“立即生效”的要求。

## 影响

- 新增 NuGet 依赖 `Microsoft.AspNetCore.Authentication.JwtBearer`（10.0.12）及其依赖 `Microsoft.IdentityModel.*`。
- 主库新增 `app_user`、`app_role`、`business` 表，`label_template` 的 `category` 改为 `business_id`，`api_client` 的 `allowed_templates` 改为业务范围和 IP 白名单；日志库新增 `op_log`，`render_log` 增加 `trace_id`、`ip`、`error_message`。
- 对接公司统一登录时新写一篇 ADR。
