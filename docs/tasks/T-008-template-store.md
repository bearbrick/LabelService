# T-008 模板存储、版本发布与缓存

状态：待验收
负责人：Claude 2026-10-09
里程碑：M3
依赖：—
设计书：§3 数据模型、§9 怎么做到；ADR 0003

## 目标

画布预设、模板、版本、素材元数据存进 SQLite；草稿和发布分开；开放接口从内存缓存取已发布版本。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Server/Data/`：EF Core DbContext、实体、迁移（表结构按设计书第 3 节，库文件 `label.db`）；连接初始化统一设置 PRAGMA（见 ADR 0003）
- 新建 `backend/src/LabelService.Server/Templates/DbTemplateStore.cs`：启动时把全部已发布版本加载到内存，发布时刷新
- 保留 `FileTemplateStore`，用配置切换：本地没有数据目录时、检查程序的部分用例仍可用示例模板
- 首次启动时如果库是空的，把 `backend/samples/templates` 导入为已发布版本（开发环境）

## 共享文件

改这些文件前先在交接记录里写明改什么；改动尽量小，单独成一个提交：

- `backend/Directory.Packages.props`：`Microsoft.EntityFrameworkCore.Sqlite`、`Microsoft.EntityFrameworkCore.Design`（生成迁移用）
- `backend/src/LabelService.Server/Program.cs`、`appsettings*.json`（新增 `LabelService:Data:Directory`）
- `verify.ps1`：临时启动的服务改用临时数据目录，跑完删除
- `AGENTS.md`：第 1 节当前阶段、第 9 节 SQLite 和迁移的坑

## 验收标准

- [x] 数据目录可配置，默认程序目录下 `data/`；启动时自动建库、执行迁移
- [x] 时间列存 UTC `DateTime`，不用 `DateTimeOffset`（ADR 0003 的 Provider 限制）
- [x] 发布时校验模板：绑定只引用已定义字段、条码旋转是直角、用样例值试渲染一次并计时（> 50 ms 给提示，不阻止发布）
- [x] 发布后的版本内容不可修改；同一模板同一时间最多一个草稿
- [x] 开放接口取模板不访问数据库（检查程序用计数或替身验证）
- [x] 发布新版本后立即生效（单实例，不需要跨实例同步）
- [x] 检查程序用临时数据目录，跑完删除
- [x] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 管理界面（见 T-014）
- 多实例缓存同步（ADR 0003：前期单实例）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
- 2026-10-09 · Claude · 完成，待验收。
  - 做了什么：`Server/Data/`（四张表 canvas_preset、label_template、label_template_version、asset 的实体和 DbContext，列名按设计书用下划线；初始迁移；建库时写入 8 个默认画布预设，和设计器一致；`SqlitePragmaInterceptor` 统一设置 PRAGMA）。`Server/Templates/`：`DbTemplateStore`（开放接口用的内存缓存，读不加锁，发布时复制出新快照替换）、`TemplateRepository`（保存草稿、发布、放弃草稿、回滚到旧版本、启动加载）、`TemplatePublishValidator`（发布前检查 + 试渲染计时）、`TemplateStorage`（注册和启动初始化：建目录、迁移、开发环境导入示例、加载）。`FileTemplateStore` 保留，`LabelService:Templates:Source=Files` 时使用。检查程序新增 `StorageChecks`（24 项），`ServerChecks` 改用临时数据目录；`verify.ps1` 启动的服务也用临时数据目录。
  - 依赖：`Microsoft.EntityFrameworkCore.Sqlite`、`Microsoft.EntityFrameworkCore.Design` 10.0.12（2026-09-08 发布）。用 EF Core 是 ADR 0003 定的，没有另写 ADR。Design 只在生成迁移时用（PrivateAssets=all）。
  - 设计上的取舍：① 草稿的版本号就是“最新发布版本 + 1”，加上 (template_id, version_no) 唯一索引，数据库层面保证同一模板最多一个草稿；② 版本的 status 是并发令牌，两人同时发布或一人保存时另一人刚发布，后到的失败（`TemplateStoreException`，提示重新打开）；③ `LabelDbContext` 保存时拦截对已发布版本的修改和删除；④ 启动加载时某个版本 JSON 解析失败只记错误日志并跳过，不让整个服务起不来；⑤ 必填字段没有样例值时，试渲染用占位值并给提示，不阻止发布；⑥ 直角检查只针对一维码（和设计器一致），二维码不限制；⑦ EF Core 的 SQL 命令日志调到 Warning，否则每条 SQL 都写 Information 日志。
  - 新定的规则（请确认）：模板编码只能是字母开头、字母数字下划线连字符、最长 64（编码会出现在接口路径和下载文件名里）。原来的契约没有写编码规则。
  - 没做：管理接口（T-014 直接调 `TemplateRepository`，异常 `TemplateStoreException` 转 400/409）；画布预设和素材只有表，没有增删改（T-014）；调用日志库 label-log.db（T-009）；每日 VACUUM INTO 备份（T-017）；“试渲染超过 50 ms 提示”没有检查用例（没法稳定造出慢模板）。
  - 遇到的坑：Windows 上删临时库前必须 `SqliteConnection.ClearAllPools()`；WebApplicationFactory 覆盖的配置要在用到时才读（注册时读不到），所以存储配置都通过 IOptions 延迟读取；Git Bash 的 `sed -i` 会把 CRLF 改成 LF。
  - 性能：Rendering 没改，不需要基准。开放接口取模板仍是一次内存字典查找（改成 FrozenDictionary）。冷启动第一次试渲染约 26 ms。
  - 留给下一位：T-009 可以照 `LabelDbContext` 的做法给 label-log.db 建第二个 DbContext；T-014 拿模板列表、草稿、发布、回滚直接用 `TemplateRepository`，画布预设的增删改要补。
- 2026-10-09 · Claude · 认领。计划：EF Core + SQLite 实现画布预设、模板、版本、素材元数据四张表；草稿和发布分开，已发布版本启动时整体加载进内存，发布时追加，开放接口只读内存。
- 2026-10-09 · Claude · 数据库已定为 SQLite、单实例部署（ADR 0003），状态改为待认领。
- 2026-10-09 · Claude · 标为阻塞：等数据库选型。确定后把状态改为待认领。
